using System;
using System.IO;

namespace PAS.Services;

/// <summary>
/// Сервис для централизованного управления путями приложения
/// </summary>
public class AppPaths
{
    public string BaseFolder { get; }
    public string LogFilePath { get; }
    public string CacheFilePath { get; }

    public AppPaths()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        BaseFolder = Path.Combine(appDataPath, "PAS");
        
        // Создаем папку сразу
        if (!Directory.Exists(BaseFolder))
        {
            Directory.CreateDirectory(BaseFolder);
        }

        LogFilePath = Path.Combine(BaseFolder, "PAS.log");
        CacheFilePath = Path.Combine(BaseFolder, "scan_cache.json");
    }
}
