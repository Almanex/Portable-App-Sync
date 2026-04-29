using System.Collections.Generic;

namespace PAS.Models;

/// <summary>
/// Результат операции экспорта
/// </summary>
public class ExportResult
{
    /// <summary>
    /// Успешность операции
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Сообщение о результате
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Путь к созданному файлу/папке
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Количество успешно обработанных приложений
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Количество приложений с ошибками
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Количество пропущенных приложений
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Количество приложений, добавленных в online fallback script.
    /// </summary>
    public int FallbackCount { get; set; }

    /// <summary>
    /// Путь к online fallback script, если он был создан.
    /// </summary>
    public string OnlineFallbackScriptPath { get; set; } = string.Empty;

    /// <summary>
    /// Список приложений, которые не удалось обработать
    /// </summary>
    public List<string> FailedApps { get; set; } = new();

    /// <summary>
    /// Список приложений, которые были пропущены
    /// </summary>
    public List<string> SkippedApps { get; set; } = new();
}
