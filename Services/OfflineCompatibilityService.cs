using System;
using System.Collections.Generic;
using System.Linq;

namespace PAS.Services;

/// <summary>
/// Сервис для определения совместимости приложений с оффлайн-загрузкой
/// </summary>
public class OfflineCompatibilityService
{
    private readonly LoggingService _logger;
    
    // Известные приложения, которые не поддерживают winget download
    private static readonly HashSet<string> OfflineIncompatibleApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.VisualStudioCode",
        "Git.Git",
        "Google.AndroidStudio",
        "Microsoft.VisualStudio.Community",
        "Microsoft.VisualStudio.Professional",
        "Microsoft.VisualStudio.Enterprise",
        "Microsoft.VisualStudio.2019.BuildTools",
        "Microsoft.VisualStudio.2022.BuildTools",
        // Добавьте другие известные несовместимые приложения
    };

    public OfflineCompatibilityService(LoggingService logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Проверка, поддерживает ли приложение оффлайн-загрузку
    /// </summary>
    public bool SupportsOfflineDownload(string wingetId)
    {
        if (string.IsNullOrWhiteSpace(wingetId))
            return false;

        var isCompatible = !OfflineIncompatibleApps.Contains(wingetId) && !IsMicrosoftStoreProductId(wingetId);
        
        if (!isCompatible)
        {
            _logger.LogInfo($"Приложение {wingetId} не поддерживает оффлайн-загрузку");
        }
        
        return isCompatible;
    }

    /// <summary>
    /// Получение списка несовместимых приложений из выбранных
    /// </summary>
    public List<string> GetIncompatibleApps(IEnumerable<string> wingetIds)
    {
        return wingetIds.Where(id => !SupportsOfflineDownload(id)).ToList();
    }

    /// <summary>
    /// Получение количества несовместимых приложений
    /// </summary>
    public int GetIncompatibleCount(IEnumerable<string> wingetIds)
    {
        return GetIncompatibleApps(wingetIds).Count;
    }

    public static bool IsMicrosoftStoreProductId(string wingetId)
    {
        return wingetId.Length >= 10 &&
               (wingetId.StartsWith("9", StringComparison.OrdinalIgnoreCase) ||
                wingetId.StartsWith("XP", StringComparison.OrdinalIgnoreCase)) &&
               wingetId.All(char.IsLetterOrDigit);
    }
}
