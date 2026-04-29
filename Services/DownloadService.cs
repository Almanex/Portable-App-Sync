using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PAS.Models;

namespace PAS.Services;

/// <summary>
/// Прогресс загрузки
/// </summary>
public class DownloadProgress
{
    public int TotalApps { get; set; }
    public int CurrentAppIndex { get; set; }
    public string CurrentAppName { get; set; } = string.Empty;
    public int OverallProgress => TotalApps > 0 ? (CurrentAppIndex * 100) / TotalApps : 0;
}

/// <summary>
/// Сервис загрузки дистрибутивов
/// </summary>
public class DownloadService
{
    private readonly WingetService _wingetService;
    private readonly ScriptGeneratorService _scriptGenerator;
    private readonly LoggingService _logger;

    public DownloadService(WingetService wingetService, ScriptGeneratorService scriptGenerator, LoggingService logger)
    {
        _wingetService = wingetService;
        _scriptGenerator = scriptGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Загрузка пакетов в указанную папку с поддержкой отмены.
    /// Для приложений, которые нельзя скачать оффлайн, рядом создается online fallback script.
    /// </summary>
    public async Task<ExportResult> DownloadPackages(
        List<InstalledApp> apps,
        string targetFolder,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new ExportResult
        {
            OutputPath = targetFolder
        };

        try
        {
            var l = LocalizationManager.Instance;
            _logger.LogInfo($"Начало загрузки {apps.Count} пакетов в {targetFolder}");

            Directory.CreateDirectory(targetFolder);

            var selectedApps = apps.Where(a => a.IsSelected && a.IsAvailable).ToList();
            var totalApps = selectedApps.Count;
            var downloadedFiles = new List<string>();
            var fallbackApps = new List<InstalledApp>();

            for (int i = 0; i < selectedApps.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var app = selectedApps[i];

                progress?.Report(new DownloadProgress
                {
                    TotalApps = totalApps,
                    CurrentAppIndex = i + 1,
                    CurrentAppName = app.Name
                });

                app.Status = l.Get("AppStatusDownloading");

                if (app.IsStoreApp || OfflineCompatibilityService.IsMicrosoftStoreProductId(app.WingetId) || !app.SupportsOfflineDownload)
                {
                    var isStoreApp = app.IsStoreApp || OfflineCompatibilityService.IsMicrosoftStoreProductId(app.WingetId);
                    app.Status = isStoreApp ? l.Get("AppStatusSkippedStore") : l.Get("AppStatusSkipped");
                    result.SkippedCount++;
                    result.SkippedApps.Add(isStoreApp
                        ? l.Format("ExportSkippedStoreReason", app.Name)
                        : l.Format("ExportSkippedFallbackReason", app.Name));
                    fallbackApps.Add(app);
                    _logger.LogWarning(isStoreApp
                        ? $"Пропуск загрузки Store приложения: {app.Name}"
                        : $"Пропуск загрузки неподдерживаемого приложения: {app.Name} [{app.WingetId}]");
                    continue;
                }

                try
                {
                    var downloadedFilesList = await _wingetService.DownloadPackage(app.WingetId, targetFolder, ct);

                    if (downloadedFilesList.Count > 0)
                    {
                        downloadedFiles.AddRange(downloadedFilesList);
                        app.Status = l.Get("AppStatusDownloaded");
                        result.SuccessCount++;
                        _logger.LogInfo($"Успешно загружен: {app.Name} ({downloadedFilesList.Count} файлов)");
                    }
                    else
                    {
                        app.Status = l.Get("AppStatusDownloadError");
                        result.ErrorCount++;
                        result.FailedApps.Add(app.Name);
                        _logger.LogWarning($"Не удалось загрузить: {app.Name}");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    app.Status = l.Get("AppStatusError");
                    result.ErrorCount++;
                    result.FailedApps.Add(app.Name);
                    _logger.LogError($"Ошибка при загрузке {app.Name}", ex);
                }

                await Task.Delay(500, ct);
            }

            await CreateInstallAllScript(targetFolder, downloadedFiles);

            string? fallbackScriptPath = null;
            if (fallbackApps.Count > 0)
            {
                fallbackScriptPath = Path.Combine(targetFolder, "RestoreOnlineFallback.bat");
                var fallbackResult = await _scriptGenerator.GenerateBatchScript(fallbackApps, fallbackScriptPath);
                if (!fallbackResult.Success)
                {
                    _logger.LogWarning($"Не удалось создать online fallback script: {fallbackResult.Message}");
                    fallbackScriptPath = null;
                }
                else
                {
                    result.FallbackCount = fallbackResult.SuccessCount;
                    result.OnlineFallbackScriptPath = fallbackScriptPath;
                    _logger.LogInfo($"Создан online fallback script: {fallbackScriptPath}");
                }
            }

            result.Success = result.ErrorCount == 0;
            result.Message = l.Format("ExportCompleteSummary", result.SuccessCount, result.ErrorCount, result.SkippedCount);
            if (!string.IsNullOrWhiteSpace(fallbackScriptPath))
            {
                result.Message += $"\nOnline fallback script: {fallbackScriptPath}";
            }

            _logger.LogInfo(result.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = LocalizationManager.Instance.Format("ExportCriticalDownloadError", ex.Message);
            _logger.LogError(result.Message, ex);
        }

        return result;
    }

    /// <summary>
    /// Создание скрипта install_all.bat для установки загруженных файлов.
    /// </summary>
    private async Task CreateInstallAllScript(string targetFolder, List<string> installerFiles)
    {
        try
        {
            var scriptContent = new StringBuilder();

            var l = LocalizationManager.Instance;
            scriptContent.AppendLine("@echo off");
            scriptContent.AppendLine("chcp 65001 >nul");
            scriptContent.AppendLine("echo ========================================");
            scriptContent.AppendLine($"echo {l.Get("ScriptInstallTitle")}");
            scriptContent.AppendLine("echo ========================================");
            scriptContent.AppendLine("echo.");
            scriptContent.AppendLine();

            foreach (var file in installerFiles)
            {
                var relativeFilePath = Path.GetRelativePath(targetFolder, file);
                var extension = Path.GetExtension(file).ToLowerInvariant();

                scriptContent.AppendLine($"echo {EscapeForBatchEcho(l.Format("ScriptInstallingApp", relativeFilePath))}");

                if (extension == ".msi")
                {
                    scriptContent.AppendLine($"msiexec /i \"{relativeFilePath}\" /quiet /norestart");
                }
                else if (extension == ".exe")
                {
                    scriptContent.AppendLine($"start /wait \"\" \"{relativeFilePath}\" /S /silent /quiet /verysilent");
                }
                else if (extension is ".msix" or ".msixbundle" or ".appx" or ".appxbundle")
                {
                    scriptContent.AppendLine($"powershell -NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxPackage -Path '{relativeFilePath.Replace("'", "''")}'\"");
                }
                else if (extension == ".zip")
                {
                    scriptContent.AppendLine($"echo Archive downloaded, manual extraction required: {EscapeForBatchEcho(relativeFilePath)}");
                }

                scriptContent.AppendLine("echo.");
            }

            scriptContent.AppendLine("echo ========================================");
            scriptContent.AppendLine($"echo {l.Get("ScriptInstallDone")}");
            scriptContent.AppendLine("echo ========================================");
            scriptContent.AppendLine("echo.");
            scriptContent.AppendLine("echo If some apps were skipped during offline export, run RestoreOnlineFallback.bat with internet access.");
            scriptContent.AppendLine("pause");

            var scriptPath = Path.Combine(targetFolder, "install_all.bat");
            await File.WriteAllTextAsync(scriptPath, scriptContent.ToString(), new UTF8Encoding(false));

            _logger.LogInfo($"Создан скрипт установки: {scriptPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка при создании install_all.bat", ex);
        }
    }

    private static string EscapeForBatchEcho(string value)
    {
        return value
            .Replace("^", "^^")
            .Replace("&", "^&")
            .Replace("<", "^<")
            .Replace(">", "^>")
            .Replace("|", "^|");
    }

}
