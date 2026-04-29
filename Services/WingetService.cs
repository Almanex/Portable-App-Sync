using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PAS.Models;

namespace PAS.Services;

/// <summary>
/// РЎРµСЂРІРёСЃ РґР»СЏ РІР·Р°РёРјРѕРґРµР№СЃС‚РІРёСЏ СЃ Winget CLI
/// </summary>
public class WingetService
{
    private readonly LoggingService _logger;
    private readonly Dictionary<string, (string Name, string Id, string Source)?> _nameResolutionCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] DownloadArtifactExtensions =
    {
        ".exe", ".msi", ".msix", ".msixbundle", ".appx", ".appxbundle", ".zip"
    };
    private static readonly EnumerationOptions DownloadArtifactEnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false
    };

    /// <summary>
    /// РџР°С‚С‚РµСЂРЅ РґРѕРїСѓСЃС‚РёРјС‹С… СЃРёРјРІРѕР»РѕРІ РґР»СЏ winget ID.
    /// Р Р°Р·СЂРµС€РµРЅС‹: Р±СѓРєРІС‹, С†РёС„СЂС‹, С‚РѕС‡РєР°, РґРµС„РёСЃ, РїРѕРґС‡С‘СЂРєРёРІР°РЅРёРµ, РѕР±СЂР°С‚РЅС‹Р№ СЃР»РµС€, РїР»СЋСЃ, РјРЅРѕРіРѕС‚РѕС‡РёРµ (вЂ¦).
    /// </summary>
    private static readonly Regex SafeIdPattern = new(@"^[\w\.\-\\\+\/вЂ¦]+$", RegexOptions.Compiled);

    public WingetService(LoggingService logger)
    {
        _logger = logger;
    }

    private static bool IsArpId(string id)
    {
        return id.StartsWith("ARP\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalMsixId(string id)
    {
        return id.StartsWith("MSIX\\", StringComparison.OrdinalIgnoreCase);
    }

    public bool CanUseWingetCommands(string wingetId)
    {
        return !string.IsNullOrWhiteSpace(wingetId)
            && !IsArpId(wingetId)
            && !IsLocalMsixId(wingetId)
            && SafeIdPattern.IsMatch(wingetId);
    }

    /// <summary>
    /// РџСЂРѕРІРµСЂСЏРµС‚, С‡С‚Рѕ winget ID РЅРµ СЃРѕРґРµСЂР¶РёС‚ РѕРїР°СЃРЅС‹С… СЃРёРјРІРѕР»РѕРІ (Р·Р°С‰РёС‚Р° РѕС‚ command injection)
    /// </summary>
    private void ValidateWingetId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Winget ID РЅРµ РјРѕР¶РµС‚ Р±С‹С‚СЊ РїСѓСЃС‚С‹Рј");

        if (!SafeIdPattern.IsMatch(id))
        {
            _logger.LogError($"РќРµРґРѕРїСѓСЃС‚РёРјС‹Рµ СЃРёРјРІРѕР»С‹ РІ winget ID: '{id}'");
            throw new ArgumentException($"РќРµРґРѕРїСѓСЃС‚РёРјС‹Рµ СЃРёРјРІРѕР»С‹ РІ winget ID: '{id}'");
        }
    }

    /// <summary>
    /// РџСЂРѕРІРµСЂРєР° РЅР°Р»РёС‡РёСЏ Winget РІ СЃРёСЃС‚РµРјРµ
    /// </summary>
    public async Task<bool> CheckWingetAvailability()
    {
        try
        {
            _logger.LogInfo("РџСЂРѕРІРµСЂРєР° РЅР°Р»РёС‡РёСЏ Winget...");
            
            var result = await RunWingetCommand("--version");
            var isAvailable = result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output);
            
            if (isAvailable)
            {
                _logger.LogInfo($"Winget РЅР°Р№РґРµРЅ. Р’РµСЂСЃРёСЏ: {result.Output.Trim()}");
            }
            else
            {
                _logger.LogError("Winget РЅРµ РЅР°Р№РґРµРЅ РІ СЃРёСЃС‚РµРјРµ");
            }
            
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogError("РћС€РёР±РєР° РїСЂРё РїСЂРѕРІРµСЂРєРµ Winget", ex);
            return false;
        }
    }

    /// <summary>
    /// РџРѕР»СѓС‡РµРЅРёРµ СЃРїРёСЃРєР° СѓСЃС‚Р°РЅРѕРІР»РµРЅРЅС‹С… РїСЂРёР»РѕР¶РµРЅРёР№ С‡РµСЂРµР· winget export (JSON)
    /// Р‘РѕР»РµРµ РЅР°РґРµР¶РЅС‹Р№ РјРµС‚РѕРґ, С‡РµРј РїР°СЂСЃРёРЅРі winget list
    /// </summary>
    public async Task<List<InstalledApp>> GetInstalledAppsFromExport()
    {
        var apps = new List<InstalledApp>();
        
        try
        {
            _logger.LogInfo("РџРѕР»СѓС‡РµРЅРёРµ СЃРїРёСЃРєР° РїСЂРёР»РѕР¶РµРЅРёР№ С‡РµСЂРµР· winget export...");
            
            // РЎРѕР·РґР°РµРј РІСЂРµРјРµРЅРЅС‹Р№ С„Р°Р№Р» РґР»СЏ СЌРєСЃРїРѕСЂС‚Р°
            var tempFile = Path.GetTempFileName();
            
            try
            {
                // Р­РєСЃРїРѕСЂС‚РёСЂСѓРµРј СЃРїРёСЃРѕРє РїСЂРёР»РѕР¶РµРЅРёР№ РІ JSON
                var result = await RunWingetCommand($"export -o \"{tempFile}\" --include-versions --accept-source-agreements");
                
                if (result.ExitCode != 0)
                {
                    _logger.LogError($"РћС€РёР±РєР° РІС‹РїРѕР»РЅРµРЅРёСЏ winget export: {result.Error}");
                    // Fallback to old method
                    _logger.LogInfo("РџРµСЂРµС…РѕРґ РЅР° СЂРµР·РµСЂРІРЅС‹Р№ РјРµС‚РѕРґ (winget list)...");
                    return await GetInstalledApps();
                }
                
                // Р§РёС‚Р°РµРј Рё РїР°СЂСЃРёРј JSON
                var jsonContent = await File.ReadAllTextAsync(tempFile);
                var exportData = JsonSerializer.Deserialize<WingetExportRoot>(jsonContent);
                
                if (exportData == null || exportData.Sources == null)
                {
                    _logger.LogError("РќРµ СѓРґР°Р»РѕСЃСЊ РґРµСЃРµСЂРёР°Р»РёР·РѕРІР°С‚СЊ JSON РёР· winget export");
                    return apps;
                }
                
                // РћР±СЂР°Р±Р°С‚С‹РІР°РµРј РєР°Р¶РґС‹Р№ РёСЃС‚РѕС‡РЅРёРє (winget, msstore)
                foreach (var source in exportData.Sources)
                {
                    var sourceName = source.SourceDetails?.Name ?? "unknown";
                    var isStoreSource = sourceName.Equals("msstore", StringComparison.OrdinalIgnoreCase);
                    
                    foreach (var package in source.Packages)
                    {
                        bool isSystemPackage = IsSystemPackage(package.PackageIdentifier, package.PackageIdentifier);

                        // РћРїСЂРµРґРµР»СЏРµРј, СЏРІР»СЏРµС‚СЃСЏ Р»Рё СЌС‚Рѕ Store-РїСЂРёР»РѕР¶РµРЅРёРµРј
                        bool isStoreApp = isStoreSource || 
                                         package.PackageIdentifier.StartsWith("MSIX\\", StringComparison.OrdinalIgnoreCase) ||
                                         package.PackageIdentifier.StartsWith("9", StringComparison.OrdinalIgnoreCase);
                        
                        var app = new InstalledApp
                        {
                            Name = package.PackageIdentifier, // Р‘СѓРґРµС‚ РѕР±РЅРѕРІР»РµРЅРѕ РїРѕР·Р¶Рµ С‡РµСЂРµР· winget show
                            WingetId = package.PackageIdentifier,
                            Version = package.Version,
                            Source = sourceName,
                            IsAvailable = true,
                            IsStoreApp = isStoreApp,
                            IsSystemComponent = isSystemPackage
                        };
                        
                        apps.Add(app);
                        _logger.LogInfo($"App: {app.Name}, ID: {app.WingetId}, Source: {app.Source}, IsStore: {app.IsStoreApp}");
                    }
                }
                
                _logger.LogInfo($"РќР°Р№РґРµРЅРѕ РїСЂРёР»РѕР¶РµРЅРёР№ С‡РµСЂРµР· export: {apps.Count}");
            }
            finally
            {
                // РЈРґР°Р»СЏРµРј РІСЂРµРјРµРЅРЅС‹Р№ С„Р°Р№Р»
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("РћС€РёР±РєР° РїСЂРё РїРѕР»СѓС‡РµРЅРёРё СЃРїРёСЃРєР° С‡РµСЂРµР· winget export", ex);
            // Fallback to old method
            _logger.LogInfo("РџРµСЂРµС…РѕРґ РЅР° СЂРµР·РµСЂРІРЅС‹Р№ РјРµС‚РѕРґ (winget list)...");
            return await GetInstalledApps();
        }
        
        return apps;
    }

    /// <summary>
    /// РџРѕР»СѓС‡РµРЅРёРµ СЃРїРёСЃРєР° СѓСЃС‚Р°РЅРѕРІР»РµРЅРЅС‹С… РїСЂРёР»РѕР¶РµРЅРёР№ (СЃС‚Р°СЂС‹Р№ РјРµС‚РѕРґ С‡РµСЂРµР· winget list)
    /// РСЃРїРѕР»СЊР·СѓРµС‚СЃСЏ РєР°Рє fallback РµСЃР»Рё winget export РЅРµ СЂР°Р±РѕС‚Р°РµС‚
    /// </summary>
    public async Task<List<InstalledApp>> GetInstalledApps()
    {
        var apps = new List<InstalledApp>();
        
        try
        {
            _logger.LogInfo("РџРѕР»СѓС‡РµРЅРёРµ СЃРїРёСЃРєР° СѓСЃС‚Р°РЅРѕРІР»РµРЅРЅС‹С… РїСЂРёР»РѕР¶РµРЅРёР№...");
            
            // РСЃРїРѕР»СЊР·СѓРµРј winget list РґР»СЏ РїРѕР»СѓС‡РµРЅРёСЏ СѓСЃС‚Р°РЅРѕРІР»РµРЅРЅС‹С… РїСЂРёР»РѕР¶РµРЅРёР№
            var result = await RunWingetCommand("list --accept-source-agreements");
            
            if (result.ExitCode != 0)
            {
                _logger.LogError($"РћС€РёР±РєР° РІС‹РїРѕР»РЅРµРЅРёСЏ winget list: {result.Error}");
                return apps;
            }

            // РџР°СЂСЃРёРј РІС‹РІРѕРґ winget list (С‚Р°Р±Р»РёС‡РЅС‹Р№ С„РѕСЂРјР°С‚)
            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // РџСЂРѕРїСѓСЃРєР°РµРј Р·Р°РіРѕР»РѕРІРѕРє Рё СЂР°Р·РґРµР»РёС‚РµР»СЊ
            var dataLines = lines.Skip(2).Where(l => !l.StartsWith('-')).ToList();
            
            foreach (var line in dataLines)
            {
                var app = ParseWingetListLine(line);
                if (app != null && !string.IsNullOrWhiteSpace(app.WingetId))
                {
                    apps.Add(app);
                }
            }
            
            _logger.LogInfo($"РќР°Р№РґРµРЅРѕ РїСЂРёР»РѕР¶РµРЅРёР№: {apps.Count}");
        }
        catch (Exception ex)
        {
            _logger.LogError("РћС€РёР±РєР° РїСЂРё РїРѕР»СѓС‡РµРЅРёРё СЃРїРёСЃРєР° РїСЂРёР»РѕР¶РµРЅРёР№", ex);
        }
        
        return apps;
    }

    /// <summary>
    /// РџСЂРѕРІРµСЂРєР° РґРѕСЃС‚СѓРїРЅРѕСЃС‚Рё РїР°РєРµС‚Р° РІ СЂРµРїРѕР·РёС‚РѕСЂРёРё Winget
    /// </summary>
    public async Task<bool> VerifyPackageInRepo(string wingetId)
    {
        try
        {
            if (!CanUseWingetCommands(wingetId))
                return false;

            ValidateWingetId(wingetId);
            var result = await RunWingetCommand($"show --id {wingetId} -e --accept-source-agreements");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// РџСЂРѕРІРµСЂРєР°, СЏРІР»СЏРµС‚СЃСЏ Р»Рё РїР°РєРµС‚ Store-РїСЂРёР»РѕР¶РµРЅРёРµРј
    /// РСЃРїРѕР»СЊР·СѓРµС‚ winget show РґР»СЏ РѕРїСЂРµРґРµР»РµРЅРёСЏ InstallerType Рё StoreInstallPolicy
    /// </summary>
    public async Task<bool> IsStorePackage(string wingetId)
    {
        try
        {
            ValidateWingetId(wingetId);
            _logger.LogInfo($"РџСЂРѕРІРµСЂРєР° Store-СЃС‚Р°С‚СѓСЃР° РґР»СЏ: {wingetId}");
            
            var result = await RunWingetCommand($"show --id {wingetId} -e --accept-source-agreements");
            
            if (result.ExitCode != 0)
            {
                _logger.LogInfo($"РџР°РєРµС‚ {wingetId} РЅРµ РЅР°Р№РґРµРЅ С‡РµСЂРµР· winget show");
                return false;
            }

            var output = result.Output.ToLower();

            // РџСЂРѕРІРµСЂСЏРµРј InstallerType: msstore РёР»Рё msix
            if (output.Contains("installertype:") && 
                (output.Contains("msstore") || output.Contains("msix")))
            {
                _logger.LogInfo($"{wingetId} - Store РїР°РєРµС‚ (InstallerType)");
                return true;
            }

            // РџСЂРѕРІРµСЂСЏРµРј StoreInstallPolicy: Allowed
            if (output.Contains("storeinstallpolicy:") && output.Contains("allowed"))
            {
                _logger.LogInfo($"{wingetId} - Store РїР°РєРµС‚ (StoreInstallPolicy)");
                return true;
            }

            // РџСЂРѕРІРµСЂСЏРµРј URL РЅР° apps.microsoft.com
            if (output.Contains("apps.microsoft.com"))
            {
                _logger.LogInfo($"{wingetId} - Store РїР°РєРµС‚ (URL)");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError($"РћС€РёР±РєР° РїСЂРё РїСЂРѕРІРµСЂРєРµ Store-СЃС‚Р°С‚СѓСЃР° РґР»СЏ {wingetId}", ex);
            return false;
        }
    }

    /// <summary>
    /// Р—Р°РіСЂСѓР·РєР° РїР°РєРµС‚Р°
    /// </summary>
    public async Task<List<string>> DownloadPackage(string wingetId, string targetPath, CancellationToken cancellationToken = default, IProgress<int>? progress = null)
    {
        try
        {
            if (!CanUseWingetCommands(wingetId))
            {
                _logger.LogWarning($"Skipping download for unsupported ID: {wingetId}");
                return new List<string>();
            }

            ValidateWingetId(wingetId);
            _logger.LogInfo($"Р—Р°РіСЂСѓР·РєР° РїР°РєРµС‚Р°: {wingetId}");
            
            // РЎРѕР·РґР°РµРј РїР°РїРєСѓ РµСЃР»Рё РЅРµ СЃСѓС‰РµСЃС‚РІСѓРµС‚
            Directory.CreateDirectory(targetPath);
            
            // РџРѕР»СѓС‡Р°РµРј СЃРїРёСЃРѕРє С„Р°Р№Р»РѕРІ Р”Рћ Р·Р°РіСЂСѓР·РєРё
            var existingFiles = Directory.EnumerateFiles(targetPath, "*.*", DownloadArtifactEnumerationOptions)
                .Where(f => DownloadArtifactExtensions.Contains(
                    Path.GetExtension(f),
                    StringComparer.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            // РџС‹С‚Р°РµРјСЃСЏ РёСЃРїРѕР»СЊР·РѕРІР°С‚СЊ winget download (РґРѕСЃС‚СѓРїРЅРѕ РІ РЅРѕРІС‹С… РІРµСЂСЃРёСЏС…)
            var result = await RunWingetCommand(
                $"download --id {wingetId} -e --source winget --download-directory \"{targetPath}\" --accept-source-agreements --accept-package-agreements",
                cancellationToken: cancellationToken
            );
            
            if (result.ExitCode == 0)
            {
                _logger.LogInfo($"РџР°РєРµС‚ {wingetId} СѓСЃРїРµС€РЅРѕ Р·Р°РіСЂСѓР¶РµРЅ");
                
                // РџРѕР»СѓС‡Р°РµРј СЃРїРёСЃРѕРє С„Р°Р№Р»РѕРІ РџРћРЎР›Р• Р·Р°РіСЂСѓР·РєРё
                var allFiles = Directory.EnumerateFiles(targetPath, "*.*", DownloadArtifactEnumerationOptions)
                    .Where(f => DownloadArtifactExtensions.Contains(
                        Path.GetExtension(f),
                        StringComparer.OrdinalIgnoreCase))
                    .ToList();
                
                // Р’РѕР·РІСЂР°С‰Р°РµРј С‚РѕР»СЊРєРѕ РќРћР’Р«Р• С„Р°Р№Р»С‹ (С‚Рµ, РєРѕС‚РѕСЂС‹С… РЅРµ Р±С‹Р»Рѕ РґРѕ Р·Р°РіСЂСѓР·РєРё)
                var newFiles = allFiles.Where(f => !existingFiles.Contains(f)).ToList();
                if (newFiles.Count == 0)
                {
                    newFiles = FindReusableDownloadedArtifacts(wingetId, allFiles);
                    if (newFiles.Count > 0)
                    {
                        _logger.LogInfo($"No new files were created for {wingetId}; reusing existing downloaded artifacts: {newFiles.Count}");
                    }
                }
                
                _logger.LogInfo($"Р—Р°РіСЂСѓР¶РµРЅРѕ С„Р°Р№Р»РѕРІ: {newFiles.Count}");
                return newFiles;
            }
            else
            {
                _logger.LogWarning($"РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РіСЂСѓР·РёС‚СЊ РїР°РєРµС‚ {wingetId}: {result.Error}");
                return new List<string>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"РћС€РёР±РєР° РїСЂРё Р·Р°РіСЂСѓР·РєРµ РїР°РєРµС‚Р° {wingetId}", ex);
            return new List<string>();
        }
    }

    /// <summary>
    /// РџР°СЂСЃРёРЅРі СЃС‚СЂРѕРєРё РёР· РІС‹РІРѕРґР° winget list
    /// </summary>
    private InstalledApp? ParseWingetListLine(string line)
    {
        try
        {
            // Р¤РѕСЂРјР°С‚: Name   Id   Version   Available   Source
            // РСЃРїРѕР»СЊР·СѓРµРј СЂРµРіСѓР»СЏСЂРЅРѕРµ РІС‹СЂР°Р¶РµРЅРёРµ РґР»СЏ СЂР°Р·Р±РѕСЂР°
            var parts = Regex.Split(line.Trim(), @"\s{2,}");
            
            // РРіРЅРѕСЂРёСЂСѓРµРј СЃС‚СЂРѕРєРё РїСЂРѕРіСЂРµСЃСЃ-Р±Р°СЂРѕРІ Рё СЃРїРёРЅРЅРµСЂРѕРІ
            if (line.Contains('\u2588') || line.Contains('\u2591') || line.Contains('\u2593') || line.Contains('\u2592') || 
                (line.StartsWith('[') && line.EndsWith(']')) ||
                line.Trim().Length < 3) // РЎР»РёС€РєРѕРј РєРѕСЂРѕС‚РєРёРµ СЃС‚СЂРѕРєРё (СЃРїРёРЅРЅРµСЂС‹)
            {
                return null;
            }

            if (parts.Length < 2)
                return null;

            // РРіРЅРѕСЂРёСЂСѓРµРј СЃС‚СЂРѕРєСѓ Р·Р°РіРѕР»РѕРІРєРѕРІ
            if ((parts[0].Trim() == "Name" && parts[1].Trim() == "Id") ||
                (parts[0].Trim() == "РРјСЏ" && parts[1].Trim() == "РР”"))
            {
                return null;
            }

            string name = parts[0].Trim();
            string id = string.Empty;
            string version = string.Empty;
            string source = "winget";
            
            // РЎРїРµС†РёР°Р»СЊРЅС‹Р№ СЃР»СѓС‡Р°Р№: РµСЃР»Рё Part[0] СЃРѕРґРµСЂР¶РёС‚ Рё СЃРєРѕР±РєРё, Рё С‚РѕС‡РєРё, РІРѕР·РјРѕР¶РЅРѕ РёРјСЏ Рё ID СЃР»РёРїР»РёСЃСЊ
            // РџСЂРёРјРµСЂ: "Microsoft Visual Studio Code (User) Microsoft.VisualStudioCode"
            // РР»Рё РѕР±СЂРµР·Р°РЅРЅС‹Р№: "Microsoft Visual Studio Code (UseвЂ¦ Microsoft.VisualStudioCode"
            if (name.Contains('(') && name.Contains('.'))
            {
                // РС‰РµРј РїРѕСЃР»РµРґРЅРµРµ РІС…РѕР¶РґРµРЅРёРµ Р·Р°РєСЂС‹РІР°СЋС‰РµР№ СЃРєРѕР±РєРё РёР»Рё РјРЅРѕРіРѕС‚РѕС‡РёСЏ
                int lastCloseParen = name.LastIndexOf(')');
                int ellipsisPos = name.IndexOf('\u2026');
                
                int splitPos = -1;
                if (ellipsisPos > 0)
                {
                    // Р•СЃР»Рё РµСЃС‚СЊ РјРЅРѕРіРѕС‚РѕС‡РёРµ, СЂР°Р·РґРµР»СЏРµРј РїРѕСЃР»Рµ РЅРµРіРѕ
                    splitPos = ellipsisPos + 1;
                }
                else if (lastCloseParen > 0 && lastCloseParen < name.Length - 1)
                {
                    // Р•СЃР»Рё РµСЃС‚СЊ Р·Р°РєСЂС‹РІР°СЋС‰Р°СЏ СЃРєРѕР±РєР°, СЂР°Р·РґРµР»СЏРµРј РїРѕСЃР»Рµ РЅРµРµ
                    splitPos = lastCloseParen + 1;
                }
                
                if (splitPos > 0 && splitPos < name.Length)
                {
                    // Р’СЃРµ РїРѕСЃР»Рµ С‚РѕС‡РєРё СЂР°Р·РґРµР»РµРЅРёСЏ - РїРѕС‚РµРЅС†РёР°Р»СЊРЅРѕ ID
                    string potentialId = name.Substring(splitPos).Trim();
                    
                    // РџСЂРѕРІРµСЂСЏРµРј, С‡С‚Рѕ СЌС‚Рѕ РїРѕС…РѕР¶Рµ РЅР° ID (СЃРѕРґРµСЂР¶РёС‚ С‚РѕС‡РєСѓ Рё РЅРµ РЅР°С‡РёРЅР°РµС‚СЃСЏ СЃ С†РёС„СЂС‹)
                    if (potentialId.Contains('.') && potentialId.Length > 0 && !char.IsDigit(potentialId[0]))
                    {
                        // Р Р°Р·РґРµР»СЏРµРј РёРјСЏ Рё ID
                        name = name.Substring(0, splitPos).Trim();
                        id = potentialId;
                        _logger.LogInfo($"DEBUG: Р Р°Р·РґРµР»РёР»Рё СЃР»РёРїС€РёРµСЃСЏ РёРјСЏ Рё ID: Name='{name}', ID='{id}'");
                    }
                }
            }
            
            // РЈРјРЅРѕРµ РѕРїСЂРµРґРµР»РµРЅРёРµ ID: РёС‰РµРј С‡Р°СЃС‚СЊ, РєРѕС‚РѕСЂР°СЏ РїРѕС…РѕР¶Р° РЅР° ID
            // ID РѕР±С‹С‡РЅРѕ СЃРѕРґРµСЂР¶РёС‚ С‚РѕС‡РєРё (РЅР°РїСЂРёРјРµСЂ, Microsoft.VisualStudioCode) Рё РЅРµ РЅР°С‡РёРЅР°РµС‚СЃСЏ СЃРѕ СЃРєРѕР±РєРё
            for (int i = 1; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                
                // РџСЂРѕРїСѓСЃРєР°РµРј С‡Р°СЃС‚Рё РІ СЃРєРѕР±РєР°С… (РЅР°РїСЂРёРјРµСЂ, "(User)")
                if (part.StartsWith("(") && part.EndsWith(")"))
                {
                    // Р”РѕР±Р°РІР»СЏРµРј Рє РёРјРµРЅРё
                    name += " " + part;
                    continue;
                }
                
                // Р•СЃР»Рё ID РµС‰Рµ РЅРµ РЅР°Р№РґРµРЅ Рё С‡Р°СЃС‚СЊ РїРѕС…РѕР¶Р° РЅР° ID
                // ID СЃРѕРґРµСЂР¶РёС‚ С‚РѕС‡РєСѓ РёР»Рё СЃР»РµС€, РЅРѕ РќР• РЅР°С‡РёРЅР°РµС‚СЃСЏ СЃ С†РёС„СЂС‹ (СЌС‚Рѕ РѕС‚Р»РёС‡Р°РµС‚ РµРіРѕ РѕС‚ РІРµСЂСЃРёРё)
                if (string.IsNullOrEmpty(id))
                {
                    bool looksLikeId = (part.Contains('.') || part.Contains('\\')) && !char.IsDigit(part[0]);
                    // РўР°РєР¶Рµ СЃС‡РёС‚Р°РµРј ID'РѕРј СЃС‚СЂРѕРєРё, РЅР°С‡РёРЅР°СЋС‰РёРµСЃСЏ СЃ С†РёС„СЂС‹ 9 (Store app IDs С‚РёРїР° 9NT1R1C2HH7J)
                    bool isStoreAppId = part.Length > 5 && part.StartsWith("9") && part.All(c => char.IsLetterOrDigit(c));
                    
                    if (looksLikeId || isStoreAppId)
                    {
                        id = part;
                        continue;
                    }
                }
                
                // Р•СЃР»Рё ID СѓР¶Рµ РЅР°Р№РґРµРЅ, СЃР»РµРґСѓСЋС‰Р°СЏ С‡Р°СЃС‚СЊ - РІРµСЂРѕСЏС‚РЅРѕ РІРµСЂСЃРёСЏ
                if (!string.IsNullOrEmpty(id) && string.IsNullOrEmpty(version))
                {
                    // РџСЂРѕРІРµСЂСЏРµРј, С‡С‚Рѕ СЌС‚Рѕ РїРѕС…РѕР¶Рµ РЅР° РІРµСЂСЃРёСЋ (РЅР°С‡РёРЅР°РµС‚СЃСЏ СЃ С†РёС„СЂС‹)
                    if (char.IsDigit(part[0]))
                    {
                        version = part;
                        continue;
                    }
                }
                
                // Р•СЃР»Рё СЌС‚Рѕ "winget" РёР»Рё "msstore", СЌС‚Рѕ РёСЃС‚РѕС‡РЅРёРє
                if (part.ToLower() == "winget" || part.ToLower() == "msstore")
                {
                    source = part.ToLower();
                    break;
                }
            }
            
            // Р•СЃР»Рё ID РЅРµ РЅР°Р№РґРµРЅ, РёСЃРїРѕР»СЊР·СѓРµРј СЃС‚Р°СЂСѓСЋ Р»РѕРіРёРєСѓ
            if (string.IsNullOrEmpty(id) && parts.Length > 1)
            {
                id = parts[1].Trim();
            }
            if (string.IsNullOrEmpty(version) && parts.Length > 2)
            {
                version = parts[2].Trim();
            }

            // Р¤РёР»СЊС‚СЂР°С†РёСЏ СЃРёСЃС‚РµРјРЅС‹С… РєРѕРјРїРѕРЅРµРЅС‚РѕРІ Рё РґСѓР±Р»РёРєР°С‚РѕРІ СЂР°РЅС‚Р°Р№РјРѕРІ
            bool isSystemPackage = IsSystemPackage(name, id);
            
            // Р•СЃР»Рё СЌС‚Рѕ ARP ID (Add/Remove Programs), РїСЂРѕРїСѓСЃРєР°РµРј РµРіРѕ
            // ARP ID РІС‹РіР»СЏРґСЏС‚ РєР°Рє "ARP\Machine\X64\Git_is1" Рё РЅРµ СЂР°Р±РѕС‚Р°СЋС‚ РґР»СЏ СѓСЃС‚Р°РЅРѕРІРєРё
            // Р’РјРµСЃС‚Рѕ СЌС‚РѕРіРѕ Р±СѓРґРµРј РёСЃРєР°С‚СЊ РїСЂР°РІРёР»СЊРЅС‹Р№ winget ID
            // Р•СЃР»Рё СЌС‚Рѕ ARP ID (Add/Remove Programs), РїРѕРјРµС‡Р°РµРј РµРіРѕ
            // ARP ID РІС‹РіР»СЏРґСЏС‚ РєР°Рє "ARP\Machine\X64\Git_is1"
            if (id.StartsWith("ARP\\", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInfo($"РћР±РЅР°СЂСѓР¶РµРЅ Р»РѕРєР°Р»СЊРЅС‹Р№ ARP ID РґР»СЏ '{name}': {id}. РџСЂРёР»РѕР¶РµРЅРёРµ РґРѕР±Р°РІР»РµРЅРѕ, РЅРѕ РјРѕР¶РµС‚ РїРѕС‚СЂРµР±РѕРІР°С‚СЊ СѓС‚РѕС‡РЅРµРЅРёСЏ ID РґР»СЏ РїРµСЂРµСѓСЃС‚Р°РЅРѕРІРєРё.");
            }

            // РџРѕРїС‹С‚РєР° РІРѕСЃСЃС‚Р°РЅРѕРІР»РµРЅРёСЏ РґР°РЅРЅС‹С… РїСЂРё СЃР»РёРїС€РёС…СЃСЏ РєРѕР»РѕРЅРєР°С… (РєРѕРіРґР° parts.Length < 4)
            // Р§Р°СЃС‚Рѕ Р±С‹РІР°РµС‚ РґР»СЏ Store-РїСЂРёР»РѕР¶РµРЅРёР№, РіРґРµ ID РґР»РёРЅРЅС‹Р№ Рё РѕР±СЂРµР·Р°РµС‚СЃСЏ, Р° Source РЅРµ РІР»РµР·Р°РµС‚
            if (parts.Length < 4 && id.Contains(" "))
            {
                // Р•СЃР»Рё ID СЃРѕРґРµСЂР¶РёС‚ РїСЂРѕР±РµР», РІРѕР·РјРѕР¶РЅРѕ СЌС‚Рѕ ID Рё Version СЃР»РёРїР»РёСЃСЊ
                // РџСЂРёРјРµСЂ: "MSIX\Microsoft.WindowsCalculator_вЂ¦ 11.2508.1.0"
                var idParts = id.Split(' ');
                if (idParts.Length >= 2)
                {
                    id = idParts[0].Trim();
                    // РћСЃС‚Р°Р»СЊРЅРѕРµ СЃС‡РёС‚Р°РµРј РІРµСЂСЃРёРµР№
                    version = string.Join(" ", idParts.Skip(1)).Trim();
                }
            }

            // РћРїСЂРµРґРµР»РµРЅРёРµ Store-РїСЂРёР»РѕР¶РµРЅРёСЏ РїРѕ ID
            // MSIX РїР°РєРµС‚С‹ РѕР±С‹С‡РЅРѕ РёР· Store
            bool isStoreApp = false;
            if (id.StartsWith("MSIX\\", StringComparison.OrdinalIgnoreCase) ||
                OfflineCompatibilityService.IsMicrosoftStoreProductId(id))
            {
                isStoreApp = true;
                source = "msstore";
            }

            // РЎС‚Р°РЅРґР°СЂС‚РЅРѕРµ РѕРїСЂРµРґРµР»РµРЅРёРµ Source РёР· parts
            // Р•СЃР»Рё РјС‹ РІСЂСѓС‡РЅСѓСЋ РёР·РІР»РµРєР»Рё ID РёР· Part[0], С‚Рѕ source РјРѕР¶РµС‚ Р±С‹С‚СЊ РІ Part[1] РёР»Рё Part[2]
            for (int i = parts.Length - 1; i >= 1; i--)
            {
                var part = parts[i].Trim().ToLower();
                if (part == "msstore" || part == "winget")
                {
                    source = part;
                    isStoreApp = (part == "msstore");
                    break;
                }
            }
            
            // Р•СЃР»Рё РѕРїСЂРµРґРµР»РёР»Рё РєР°Рє Store РїРѕ ID, РїСЂРёРЅСѓРґРёС‚РµР»СЊРЅРѕ СЃС‚Р°РІРёРј msstore
            if ((isStoreApp || OfflineCompatibilityService.IsMicrosoftStoreProductId(id)) && source == "winget")
            {
                source = "msstore";
            }

            var app = new InstalledApp
            {
                Name = name,
                WingetId = id,
                Version = version,
                Source = source,
                IsAvailable = true,
                IsStoreApp = source == "msstore",
                IsSystemComponent = isSystemPackage
            };

            return app;
        }
        catch (Exception ex)
        {
            _logger.LogError($"РћС€РёР±РєР° РїСЂРё РїР°СЂСЃРёРЅРіРµ СЃС‚СЂРѕРєРё '{line}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// РџСЂРѕРІРµСЂСЏРµС‚, СЏРІР»СЏРµС‚СЃСЏ Р»Рё РїР°РєРµС‚ СЃРёСЃС‚РµРјРЅС‹Рј РєРѕРјРїРѕРЅРµРЅС‚РѕРј РёР»Рё С‚РµС…РЅРёС‡РµСЃРєРѕР№ Р·Р°РІРёСЃРёРјРѕСЃС‚СЊСЋ,
    /// РєРѕС‚РѕСЂСѓСЋ РЅРµ СЃР»РµРґСѓРµС‚ РїРѕРєР°Р·С‹РІР°С‚СЊ РїРѕР»СЊР·РѕРІР°С‚РµР»СЋ.
    /// </summary>
    private bool IsSystemPackage(string name, string id)
    {
        // 0. РџСЂСЏРјРѕРµ РёСЃРєР»СЋС‡РµРЅРёРµ РґР»СЏ РІР°Р¶РЅС‹С… РїСЂРёР»РѕР¶РµРЅРёР№ Microsoft, РєРѕС‚РѕСЂС‹Рµ РЅРµ РґРѕР»Р¶РЅС‹ СЃРєСЂС‹РІР°С‚СЊСЃСЏ РІ "СЃРёСЃС‚РµРјРЅС‹Рµ"
        string[] userAppExceptions = new[]
        {
            "Microsoft.VisualStudioCode",
            "Microsoft.Teams",
            "Microsoft.PowerToys",
            "Microsoft.PowerShell",
            "Microsoft.Office",
            "Microsoft.Edge",
            "Microsoft.OneDrive",
            "Microsoft.SQLServer",
            "Microsoft.Bing",
            "Microsoft.Skype",
            "Microsoft.To Do",
            "Microsoft.RemoteDesktop"
        };
        
        foreach (var ex in userAppExceptions)
        {
            if (id.StartsWith(ex, StringComparison.OrdinalIgnoreCase) || 
                name.Contains(ex, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // 1. РџСЂРѕРІРµСЂРєР° РїРѕ РїРѕРґСЃС‚СЂРѕРєРµ РІ ID (С‚РµС…РЅРёС‡РµСЃРєРёРµ Р·Р°РІРёСЃРёРјРѕСЃС‚Рё Рё СЃРёСЃС‚РµРјРЅС‹Рµ РїР°РєРµС‚С‹)
        string[] systemKeywords = new[]
        {
            // Windows App Runtime Рё Р·Р°РІРёСЃРёРјРѕСЃС‚Рё
            "WindowsAppRuntime",
            "WindowsWorkload",
            // UI С„СЂРµР№РјРІРѕСЂРєРё
            "Microsoft.UI.Xaml",
            "Microsoft.VCLibs",
            "Microsoft.DirectX",
            // Visual C++ Redistributable (РІСЃРµ РІРµСЂСЃРёРё)
            "Microsoft.VCRedist",
            // .NET SDK Рё Runtime
            "Microsoft.DotNet",
            // Windows SDK
            "Microsoft.WindowsSDK",
            // Winget / App Installer
            "Microsoft.AppInstaller",
            // РЎРёСЃС‚РµРјРЅС‹Рµ MPI Рё РЅРёР·РєРѕСѓСЂРѕРІРЅРµРІС‹Рµ РєРѕРјРїРѕРЅРµРЅС‚С‹
            "Microsoft.msmpi",
            // GPU РґСЂР°Р№РІРµСЂС‹ Рё СЃРёСЃС‚РµРјРЅС‹Рµ РїР°РєРµС‚С‹
            "Nvidia.CUDA",
            "Nvidia.PhysX",
            // РџРѕРґСЃРёСЃС‚РµРјС‹ WinML Рё ML Runtime
            "UUP",
            "PSTokenizer",
            "PSOnnxRuntime",
            "OnnxRuntime",
            "WinMLShared"
        };

        foreach (var keyword in systemKeywords)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase) || 
                id.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // 2. РџСЂРѕРІРµСЂРєР° РїРѕ РїСЂРµС„РёРєСЃР°Рј (РЅР°С‡РёРЅР°РµС‚СЃСЏ СЃ)
        // РњРЅРѕРіРёРµ СЃРёСЃС‚РµРјРЅС‹Рµ РєРѕРјРїРѕРЅРµРЅС‚С‹ РЅР°С‡РёРЅР°СЋС‚СЃСЏ СЃ Microsoft. Рё СЃРѕРґРµСЂР¶Р°С‚ СЃРїРµС†РёС„РёС‡РµСЃРєРёРµ СЃР»РѕРІР°
        if (name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) || 
            id.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
            id.StartsWith("MSIX\\Microsoft.", StringComparison.OrdinalIgnoreCase))
        {
            string[] microsoftSystemComponents = new[]
            {
                "Services.Store.Engagement",
                "StorePurchaseApp",
                "SecHealthUI",
                "AAD.BrokerPlugin",
                "AccountsControl",
                "BioEnrollment",
                "LockApp",
                "CredDialogHost",
                "ECApp",
                "AsyncTextService",
                "Windows.DevHome",
                "Windows.Photos",
                "WindowsAlarms",
                "WindowsCamera",
                "WindowsNotepad",
                "WindowsSoundRecorder",
                "WindowsStore",
                "Xbox", // РЎРєСЂС‹РІР°РµРј РІСЃРµ РєРѕРјРїРѕРЅРµРЅС‚С‹ Xbox (GamingOverlay, IdentityProvider Рё С‚.Рґ.)
                "YourPhone",
                "ZuneMusic",
                "CorporationII", // MicrosoftCorporationII...
                "Ink.Handwriting",
                "AIToolkit"
            };

            foreach (var component in microsoftSystemComponents)
            {
                if (name.Contains(component, StringComparison.OrdinalIgnoreCase) ||
                    id.Contains(component, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // РЎРєСЂС‹РІР°РµРј СЂР°СЃС€РёСЂРµРЅРёСЏ (Extensions), РµСЃР»Рё РѕРЅРё РѕС‚ Microsoft
            if (name.Contains("Extension", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("Extension", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }




    /// <summary>
    /// РџРѕРїС‹С‚РєР° РІРѕСЃСЃС‚Р°РЅРѕРІРёС‚СЊ РїРѕР»РЅС‹Р№ ID РґР»СЏ Store-РїСЂРёР»РѕР¶РµРЅРёР№ С‡РµСЂРµР· PowerShell
    /// </summary>
    public async Task<string> ResolveStoreAppId(string truncatedId)
    {
        try
        {
            // truncatedId format: MSIX\Microsoft.WindowsCalculator_...
            // Extract technical name: Microsoft.WindowsCalculator
            
            string technicalName = truncatedId;
            if (technicalName.StartsWith("MSIX\\", StringComparison.OrdinalIgnoreCase))
            {
                technicalName = technicalName.Substring(5);
            }
            
            // РЈРґР°Р»СЏРµРј СЃСѓС„С„РёРєСЃ (РІСЃРµ РїРѕСЃР»Рµ РїРѕСЃР»РµРґРЅРµРіРѕ РїРѕРґС‡РµСЂРєРёРІР°РЅРёСЏ, РµСЃР»Рё РѕРЅРѕ РµСЃС‚СЊ Рё С‚Р°Рј РѕР±СЂРµР·Р°РЅРѕ)
            int underscoreIndex = technicalName.IndexOf('_');
            if (underscoreIndex > 0)
            {
                technicalName = technicalName.Substring(0, underscoreIndex);
            }

            // РЈРґР°Р»СЏРµРј С‚СЂРѕРµС‚РѕС‡РёРµ, РµСЃР»Рё РѕРЅРѕ РµСЃС‚СЊ РІ РєРѕРЅС†Рµ (U+2026 РёР»Рё С‚СЂРё С‚РѕС‡РєРё)
            technicalName = technicalName.TrimEnd('\u2026', '.');
            
            // Р•СЃР»Рё РёРјСЏ СЃР»РёС€РєРѕРј РєРѕСЂРѕС‚РєРѕРµ РёР»Рё СЃС‚СЂР°РЅРЅРѕРµ, РЅРµ РїС‹С‚Р°РµРјСЃСЏ
            if (string.IsNullOrWhiteSpace(technicalName)) return truncatedId;

            // Р’Р°Р»РёРґР°С†РёСЏ РёРјРµРЅРё РїРµСЂРµРґ РїРµСЂРµРґР°С‡РµР№ РІ PowerShell (Р·Р°С‰РёС‚Р° РѕС‚ injection)
            if (!SafeIdPattern.IsMatch(technicalName))
            {
                _logger.LogWarning($"РќРµРґРѕРїСѓСЃС‚РёРјС‹Рµ СЃРёРјРІРѕР»С‹ РІ technical name: '{technicalName}', РїСЂРѕРїСѓСЃРєР°РµРј resolve");
                return truncatedId;
            }

            _logger.LogInfo($"Resolving ID for {technicalName}...");

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -NonInteractive -Command \"(Get-AppxPackage -Name {technicalName}*).Name\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var process = Process.Start(startInfo);
            if (process == null) return truncatedId;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var fullId = output.Trim();
            if (!string.IsNullOrWhiteSpace(fullId) && !fullId.Contains(" "))
            {
                // РџСЂРѕРІРµСЂСЏРµРј, С‡С‚Рѕ СЌС‚Рѕ РїРѕС…РѕР¶Рµ РЅР° PackageFamilyName
                _logger.LogInfo($"Resolved ID: {truncatedId} -> {fullId}");
                return fullId; // Р’РѕР·РІСЂР°С‰Р°РµРј С‡РёСЃС‚С‹Р№ PackageFamilyName (Р±РµР· MSIX\)
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error resolving Store ID for {truncatedId}", ex);
        }

        return truncatedId;
    }

    /// <summary>
    /// Пытается сопоставить локальное ARP/MSIX-приложение с каноническим winget ID по имени.
    /// Возвращает null, если уверенное сопоставление не найдено.
    /// </summary>
    public async Task<(string Name, string Id, string Source)?> ResolveRepositoryPackageByName(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            return null;
        }

        var normalizedSearchName = NormalizeAppNameForSearch(appName);
        if (string.IsNullOrWhiteSpace(normalizedSearchName))
        {
            return null;
        }

        if (_nameResolutionCache.TryGetValue(normalizedSearchName, out var cached))
        {
            return cached;
        }

        try
        {
            var bestCandidate = await SearchBestRepositoryCandidate(normalizedSearchName, "winget")
                ?? await SearchBestRepositoryCandidate(normalizedSearchName, null);

            if (bestCandidate.HasValue)
            {
                _logger.LogInfo($"Resolved repository ID by name: {appName} -> {bestCandidate.Value.Id}");
            }
            else
            {
                _logger.LogInfo($"No repository ID match found for: {appName}");
            }

            _nameResolutionCache[normalizedSearchName] = bestCandidate;
            return bestCandidate;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Ошибка при резолвинге repository ID для '{appName}'", ex);
            _nameResolutionCache[normalizedSearchName] = null;
            return null;
        }
    }

    private static List<string> FindReusableDownloadedArtifacts(string wingetId, IEnumerable<string> files)
    {
        var tokens = wingetId
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeArtifactToken)
            .Where(t => t.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalizedId = NormalizeArtifactToken(wingetId);
        if (normalizedId.Length >= 3)
        {
            tokens.Add(normalizedId);
        }

        return files
            .Where(file =>
            {
                var name = NormalizeArtifactToken(Path.GetFileNameWithoutExtension(file));
                return tokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));
            })
            .ToList();
    }

    private static string NormalizeArtifactToken(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    /// <summary>
    /// РџРѕР»СѓС‡РµРЅРёРµ РёРЅС„РѕСЂРјР°С†РёРё Рѕ РїСЂРёР»РѕР¶РµРЅРёРё (РёРјСЏ Рё РѕРїРёСЃР°РЅРёРµ) С‡РµСЂРµР· winget show
    /// </summary>
    public async Task<(string Name, string Description)> GetAppInfo(string wingetId)
    {
        string name = string.Empty;
        string description = string.Empty;
        
        try
        {
            if (!CanUseWingetCommands(wingetId))
            {
                return (wingetId, description);
            }

            ValidateWingetId(wingetId);
            var result = await RunWingetCommand($"show --id {wingetId} --accept-source-agreements");
            if (result.ExitCode == 0)
            {
                // РС‰РµРј РёРјСЏ РІ РїРµСЂРІРѕР№ СЃС‚СЂРѕРєРµ: РќР°Р№РґРµРЅРѕ WinRAR [RARLab.WinRAR] РёР»Рё Found WinRAR [RARLab.WinRAR]
                var nameMatch = Regex.Match(result.Output, @"(?:РќР°Р№РґРµРЅРѕ|Found)\s+(.+?)\s+\[");
                if (nameMatch.Success)
                {
                    name = nameMatch.Groups[1].Value.Trim();
                }

                var match = Regex.Match(result.Output, @"(?:РћРїРёСЃР°РЅРёРµ|Description):\s*(.+)");
                if (match.Success)
                {
                    description = match.Groups[1].Value.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"РћС€РёР±РєР° РїСЂРё РїРѕР»СѓС‡РµРЅРёРё РѕРїРёСЃР°РЅРёСЏ РґР»СЏ {wingetId}", ex);
        }
        return (string.IsNullOrEmpty(name) ? wingetId : name, description);
    }

    private static string EscapeWingetArgument(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string NormalizeAppNameForSearch(string value)
    {
        var normalized = value.Trim();

        normalized = Regex.Replace(normalized, @"\s*\((x64|x86|64-bit|32-bit|User|Machine)\)\s*$", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+\d+([\.]\d+)*.*$", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s{2,}", " ");

        return normalized.Trim(' ', '-', '_', '.');
    }

    private async Task<(string Name, string Id, string Source)?> SearchBestRepositoryCandidate(string normalizedSearchName, string? source)
    {
        var sourcePart = string.IsNullOrWhiteSpace(source) ? string.Empty : $" --source {source}";
        var result = await RunWingetCommand($"search --name {EscapeWingetArgument(normalizedSearchName)}{sourcePart} --accept-source-agreements");
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
        {
            return null;
        }

        var candidates = ParseWingetSearchResults(result.Output, source);
        return SelectBestSearchCandidate(normalizedSearchName, candidates);
    }

    private static List<(string Name, string Id, string Source)> ParseWingetSearchResults(string output, string? fallbackSource)
    {
        var results = new List<(string Name, string Id, string Source)>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith("Name", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("---", StringComparison.OrdinalIgnoreCase) ||
                line.Contains('\u2588') || line.Contains('\u2591') || line.Contains('\u2592') || line.Contains('\u2593'))
            {
                continue;
            }

            var parts = Regex.Split(line.Trim(), @"\s{2,}");
            if (parts.Length < 3)
            {
                continue;
            }

            var source = parts.Length >= 4 ? parts[^1].Trim() : (fallbackSource ?? "winget");
            results.Add((parts[0].Trim(), parts[1].Trim(), source));
        }

        return results;
    }

    private static (string Name, string Id, string Source)? SelectBestSearchCandidate(
        string normalizedSearchName,
        List<(string Name, string Id, string Source)> candidates)
    {
        var normalizedNeedle = NormalizeAppNameForSearch(normalizedSearchName).ToLowerInvariant();
        var searchToken = normalizedNeedle.Replace(" ", string.Empty);
        (string Name, string Id, string Source)? best = null;
        var bestScore = int.MinValue;

        foreach (var candidate in candidates)
        {
            var normalizedCandidateName = NormalizeAppNameForSearch(candidate.Name).ToLowerInvariant();
            var score = 0;

            if (normalizedCandidateName == normalizedNeedle)
            {
                score += 100;
            }
            else if (normalizedCandidateName.Contains(normalizedNeedle, StringComparison.OrdinalIgnoreCase) ||
                     normalizedNeedle.Contains(normalizedCandidateName, StringComparison.OrdinalIgnoreCase))
            {
                score += 40;
            }
            else
            {
                continue;
            }

            if (candidate.Source.Equals("winget", StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
            }

            if (candidate.Id.StartsWith(searchToken, StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }

            if (candidate.Id.Contains($".{searchToken}", StringComparison.OrdinalIgnoreCase) ||
                candidate.Id.EndsWith($".{searchToken}", StringComparison.OrdinalIgnoreCase))
            {
                score += 5;
            }

            if (candidate.Id.Contains(".Beta", StringComparison.OrdinalIgnoreCase) ||
                candidate.Id.Contains(".Canary", StringComparison.OrdinalIgnoreCase) ||
                candidate.Id.Contains(".Dev", StringComparison.OrdinalIgnoreCase) ||
                candidate.Id.Contains("PreRelease", StringComparison.OrdinalIgnoreCase) ||
                candidate.Name.Contains("Beta", StringComparison.OrdinalIgnoreCase) ||
                candidate.Name.Contains("Canary", StringComparison.OrdinalIgnoreCase) ||
                candidate.Name.Contains("Dev", StringComparison.OrdinalIgnoreCase) ||
                candidate.Name.Contains("Preview", StringComparison.OrdinalIgnoreCase))
            {
                score -= 25;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return bestScore >= 100 ? best : null;
    }

    /// <summary>
    /// Р’С‹РїРѕР»РЅРµРЅРёРµ РєРѕРјР°РЅРґС‹ winget СЃ С‚Р°Р№РјР°СѓС‚РѕРј.
    /// РџРѕ СѓРјРѕР»С‡Р°РЅРёСЋ РѕР¶РёРґР°РЅРёРµ вЂ” 2 РјРёРЅСѓС‚С‹; РїСЂРё Р·Р°РІРёСЃР°РЅРёРё РїСЂРѕС†РµСЃСЃ РїСЂРёРЅСѓРґРёС‚РµР»СЊРЅРѕ Р·Р°РІРµСЂС€Р°РµС‚СЃСЏ.
    /// </summary>
    private async Task<(int ExitCode, string Output, string Error)> RunWingetCommand(
        string arguments, int timeoutMs = 600_000, CancellationToken cancellationToken = default)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = processInfo };
        
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout or cancellation: terminate the process tree.
            try { process.Kill(entireProcessTree: true); }
            catch { /* process may already be closed */ }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning($"Winget command was cancelled: {arguments}");
                throw;
            }

            _logger.LogError($"Timeout ({timeoutMs / 1000}s) while executing winget {arguments}");
            return (-1, outputBuilder.ToString(), $"Timeout after {timeoutMs / 1000}s");
        }

        return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }
}
