using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PAS.Models;

namespace PAS.Services;

/// <summary>
/// Сервис сканирования системы
/// </summary>
public class SystemScanService
{
    private readonly WingetService _wingetService;
    private readonly LoggingService _logger;
    private readonly CacheService _cacheService;
    private readonly OfflineCompatibilityService _offlineCompatibility;

    public SystemScanService(WingetService wingetService, LoggingService logger, CacheService cacheService, OfflineCompatibilityService offlineCompatibility)
    {
        _wingetService = wingetService;
        _logger = logger;
        _cacheService = cacheService;
        _offlineCompatibility = offlineCompatibility;
    }

    /// <summary>
    /// Быстрое сканирование (использует кэш если доступен)
    /// </summary>
    public async Task<List<InstalledApp>> QuickScan()
    {
        _logger.LogInfo("Быстрое сканирование (с кэшем)");

        var cachedApps = await _cacheService.LoadCache();
        if (cachedApps != null)
        {
            foreach (var app in cachedApps)
            {
                if (!app.IsSystemComponent && !_wingetService.CanUseWingetCommands(app.WingetId))
                {
                    var resolved = await _wingetService.ResolveRepositoryPackageByName(app.Name);
                    if (resolved.HasValue)
                    {
                        app.Name = resolved.Value.Name;
                        app.WingetId = resolved.Value.Id;
                        app.Source = OfflineCompatibilityService.IsMicrosoftStoreProductId(resolved.Value.Id)
                            ? "msstore"
                            : resolved.Value.Source;
                        app.IsStoreApp = app.Source.Equals("msstore", StringComparison.OrdinalIgnoreCase);
                    }
                }

                app.SupportsOfflineDownload =
                    !app.IsSystemComponent &&
                    !app.IsStoreApp &&
                    !OfflineCompatibilityService.IsMicrosoftStoreProductId(app.WingetId) &&
                    _wingetService.CanUseWingetCommands(app.WingetId) &&
                    _offlineCompatibility.SupportsOfflineDownload(app.WingetId);

                ApplyExportExclusionRules(app);
            }

            var mergedCachedApps = cachedApps
                .GroupBy(app => app.WingetId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(app => _wingetService.CanUseWingetCommands(app.WingetId))
                    .ThenByDescending(app => !string.IsNullOrWhiteSpace(app.Version))
                    .First())
                .ToList();

            _logger.LogInfo($"Использован кэш: {mergedCachedApps.Count} приложений");
            return mergedCachedApps;
        }

        _logger.LogInfo("Кэш недоступен, выполняется полное сканирование");
        return await FullScan();
    }

    /// <summary>
    /// Полное сканирование (игнорирует кэш, всегда сканирует заново)
    /// </summary>
    public async Task<List<InstalledApp>> FullScan()
    {
        _logger.LogInfo("Полное сканирование (игнорируется кэш)");
        var apps = await ScanSystem();

        if (apps.Count > 0)
        {
            await _cacheService.SaveCache(apps);
        }

        return apps;
    }

    /// <summary>
    /// Сканирование системы и получение списка приложений
    /// Использует winget export (JSON) для более надежного парсинга
    /// </summary>
    private async Task<List<InstalledApp>> ScanSystem()
    {
        _logger.LogInfo("Начало сканирования системы");

        try
        {
            var wingetAvailable = await _wingetService.CheckWingetAvailability();

            if (!wingetAvailable)
            {
                _logger.LogError("Winget недоступен. Сканирование невозможно.");
                return new List<InstalledApp>();
            }

            // 1. Берем максимально полный список из `winget list`, включая локальные ARP-записи.
            var listApps = await _wingetService.GetInstalledApps();

            // 2. Берем более точные ID из `winget export`, когда они доступны.
            var exportApps = await _wingetService.GetInstalledAppsFromExport();

            var combinedApps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

            foreach (var app in listApps)
            {
                if (!string.IsNullOrWhiteSpace(app.WingetId))
                {
                    combinedApps[app.WingetId] = app;
                }
            }

            foreach (var app in exportApps)
            {
                if (string.IsNullOrWhiteSpace(app.WingetId))
                {
                    continue;
                }

                if (combinedApps.TryGetValue(app.WingetId, out var existing))
                {
                    existing.Source = app.Source;
                    existing.Version = app.Version;
                    existing.IsStoreApp = app.IsStoreApp;
                }
                else
                {
                    combinedApps[app.WingetId] = app;
                }
            }

            var filteredApps = combinedApps.Values.ToList();

            foreach (var app in filteredApps.Where(a => !a.IsSystemComponent))
            {
                if (_wingetService.CanUseWingetCommands(app.WingetId))
                {
                    continue;
                }

                var resolved = await _wingetService.ResolveRepositoryPackageByName(app.Name);
                if (resolved.HasValue)
                {
                    app.Name = resolved.Value.Name;
                    app.WingetId = resolved.Value.Id;
                    app.Source = OfflineCompatibilityService.IsMicrosoftStoreProductId(resolved.Value.Id)
                        ? "msstore"
                        : resolved.Value.Source;
                    app.IsStoreApp = app.Source.Equals("msstore", StringComparison.OrdinalIgnoreCase);
                }
            }

            filteredApps = filteredApps
                .GroupBy(app => app.WingetId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(app => _wingetService.CanUseWingetCommands(app.WingetId))
                    .ThenByDescending(app => !string.IsNullOrWhiteSpace(app.Version))
                    .First())
                .ToList();

            foreach (var app in filteredApps)
            {
                app.SupportsOfflineDownload =
                    !app.IsSystemComponent &&
                    !app.IsStoreApp &&
                    !OfflineCompatibilityService.IsMicrosoftStoreProductId(app.WingetId) &&
                    _wingetService.CanUseWingetCommands(app.WingetId) &&
                    _offlineCompatibility.SupportsOfflineDownload(app.WingetId);

                ApplyExportExclusionRules(app);
            }

            _logger.LogInfo($"Сканирование завершено. Найдено приложений: {filteredApps.Count}");
            return filteredApps;
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка при сканировании системы", ex);
            return new List<InstalledApp>();
        }
    }

    /// <summary>
    /// Получение информации о кэше
    /// </summary>
    public CacheInfo? GetCacheInfo()
    {
        return _cacheService.GetCacheInfo();
    }

    /// <summary>
    /// Очистка кэша
    /// </summary>
    public void ClearCache()
    {
        _cacheService.ClearCache();
    }

    /// <summary>
    /// Проверка, является ли приложение Store-пакетом
    /// </summary>
    public async Task<bool> CheckIfStoreApp(string wingetId)
    {
        return await _wingetService.IsStorePackage(wingetId);
    }

    /// <summary>
    /// Проверка доступности Winget в системе
    /// </summary>
    public async Task<bool> CheckWingetAvailability()
    {
        return await _wingetService.CheckWingetAvailability();
    }

    /// <summary>
    /// Получение информации о пакете (название и описание)
    /// </summary>
    public Task<(string Name, string Description)> GetAppInfo(string wingetId)
    {
        return _wingetService.GetAppInfo(wingetId);
    }

    private static void ApplyExportExclusionRules(InstalledApp app)
    {
        if (app.IsSystemComponent)
        {
            app.IsExcludedFromExport = true;
            app.ExclusionReason = "System component or technical dependency";
            return;
        }

        if (IsServiceOrUpdater(app))
        {
            app.IsExcludedFromExport = true;
            app.IsSystemComponent = true;
            app.ExclusionReason = "Service, updater, or background component";
            return;
        }

        app.IsExcludedFromExport = false;
        app.ExclusionReason = string.Empty;
    }

    private static bool IsServiceOrUpdater(InstalledApp app)
    {
        var name = app.Name ?? string.Empty;
        var id = app.WingetId ?? string.Empty;

        string[] serviceKeywords =
        {
            "Microsoft Edge Update",
            "EdgeUpdate",
            "Google Update",
            "Mozilla Maintenance Service",
            "Update Health",
            "Windows Web Experience Pack",
            "Widgets Platform Runtime",
            "Start Experiences",
            "WindowsAppRuntime",
            "VCLibs",
            "Runtime",
            "Redistributable"
        };

        return serviceKeywords.Any(keyword =>
            name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            id.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
