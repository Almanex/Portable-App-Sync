using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PAS.Models;

namespace PAS.Services;

/// <summary>
/// Сервис кэширования результатов сканирования
/// </summary>
public class CacheService
{
    private readonly LoggingService _logger;
    private readonly string _cacheFilePath;
    private readonly TimeSpan _cacheExpiration;

    /// <summary>
    /// Максимальный размер кэш-файла (50 МБ). Защита от OOM при чтении подменённого файла.
    /// </summary>
    private const long MaxCacheFileSize = 50 * 1024 * 1024;

    /// <summary>
    /// Паттерн допустимых символов для winget ID (аналогичен WingetService.SafeIdPattern).
    /// </summary>
    private static readonly Regex SafeIdPattern = new(@"^[\w\.\-\\\+\/…]+$", RegexOptions.Compiled);

    public CacheService(LoggingService logger, AppPaths appPaths, TimeSpan? cacheExpiration = null)
    {
        _logger = logger;
        _cacheExpiration = cacheExpiration ?? TimeSpan.FromHours(24);
        _cacheFilePath = appPaths.CacheFilePath;
    }

    /// <summary>
    /// Сохранение результатов сканирования в кэш
    /// </summary>
    public async Task SaveCache(List<InstalledApp> apps)
    {
        try
        {
            var cache = new ScanCache
            {
                Timestamp = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_cacheExpiration),
                Apps = apps
            };

            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(_cacheFilePath, json);
            _logger.LogInfo($"Кэш сохранен: {apps.Count} приложений, истекает {cache.ExpiresAt:g}");
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка при сохранении кэша", ex);
        }
    }

    /// <summary>
    /// Загрузка результатов из кэша
    /// </summary>
    public async Task<List<InstalledApp>?> LoadCache()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
            {
                _logger.LogInfo("Файл кэша не найден");
                return null;
            }

            // Защита от OOM: проверяем размер файла перед чтением
            var fileInfo = new System.IO.FileInfo(_cacheFilePath);
            if (fileInfo.Length > MaxCacheFileSize)
            {
                _logger.LogError($"Файл кэша слишком большой ({fileInfo.Length / 1024 / 1024} МБ), удаляем");
                ClearCache();
                return null;
            }

            var json = await File.ReadAllTextAsync(_cacheFilePath);
            
            ScanCache? cache;
            try
            {
                cache = JsonSerializer.Deserialize<ScanCache>(json);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError("Повреждённый JSON в кэше, удаляем", jsonEx);
                ClearCache();
                return null;
            }

            if (cache == null)
            {
                _logger.LogError("Не удалось десериализовать кэш");
                return null;
            }

            if (!IsCacheValid(cache))
            {
                _logger.LogInfo($"Кэш устарел (создан {cache.Timestamp:g}, истек {cache.ExpiresAt:g})");
                return null;
            }

            // Валидация WingetId каждого приложения (защита от command injection через подменённый кэш)
            var validApps = new List<InstalledApp>();
            int skipped = 0;
            foreach (var app in cache.Apps)
            {
                if (string.IsNullOrWhiteSpace(app.WingetId) || !SafeIdPattern.IsMatch(app.WingetId))
                {
                    skipped++;
                    _logger.LogWarning($"Пропущено приложение с недопустимым ID из кэша: '{app.WingetId}'");
                    continue;
                }
                validApps.Add(app);
            }

            if (skipped > 0)
            {
                _logger.LogWarning($"Отфильтровано {skipped} приложений с недопустимыми ID");
            }

            _logger.LogInfo($"Кэш загружен: {validApps.Count} приложений (возраст: {DateTime.UtcNow - cache.Timestamp:hh\\:mm\\:ss})");
            return validApps;
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка при загрузке кэша", ex);
            return null;
        }
    }

    /// <summary>
    /// Проверка валидности кэша
    /// </summary>
    public bool IsCacheValid()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
                return false;

            var json = File.ReadAllText(_cacheFilePath);
            var cache = JsonSerializer.Deserialize<ScanCache>(json);
            
            return cache != null && IsCacheValid(cache);
        }
        catch
        {
            return false;
        }
    }

    private bool IsCacheValid(ScanCache cache)
    {
        return DateTime.UtcNow < cache.ExpiresAt;
    }

    /// <summary>
    /// Очистка кэша
    /// </summary>
    public void ClearCache()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                File.Delete(_cacheFilePath);
                _logger.LogInfo("Кэш очищен");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка при очистке кэша", ex);
        }
    }

    /// <summary>
    /// Получение информации о кэше
    /// </summary>
    public CacheInfo? GetCacheInfo()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
                return null;

            var json = File.ReadAllText(_cacheFilePath);
            var cache = JsonSerializer.Deserialize<ScanCache>(json);

            if (cache == null)
                return null;

            return new CacheInfo
            {
                Timestamp = cache.Timestamp,
                ExpiresAt = cache.ExpiresAt,
                AppCount = cache.Apps.Count,
                IsValid = IsCacheValid(cache),
                Age = DateTime.UtcNow - cache.Timestamp
            };
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Модель кэша сканирования
/// </summary>
public class ScanCache
{
    public DateTime Timestamp { get; set; }
    public DateTime ExpiresAt { get; set; }
    public List<InstalledApp> Apps { get; set; } = new();
}

/// <summary>
/// Информация о кэше
/// </summary>
public class CacheInfo
{
    public DateTime Timestamp { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int AppCount { get; set; }
    public bool IsValid { get; set; }
    public TimeSpan Age { get; set; }
}
