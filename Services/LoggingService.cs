using System;
using System.IO;
using System.Text;

namespace PAS.Services;

/// <summary>
/// Сервис логирования операций
/// </summary>
public class LoggingService
{
    private readonly string _logFilePath;
    private readonly object _lockObject = new();

    /// <summary>
    /// Максимальный размер лог-файла (5 МБ). При превышении выполняется ротация.
    /// </summary>
    private const long MaxLogSize = 5 * 1024 * 1024;

    public LoggingService(AppPaths appPaths)
    {
        _logFilePath = appPaths.LogFilePath;
    }

    /// <summary>
    /// Записать информационное сообщение
    /// </summary>
    public void LogInfo(string message)
    {
        WriteLog("INFO", message);
    }

    /// <summary>
    /// Записать предупреждение
    /// </summary>
    public void LogWarning(string message)
    {
        WriteLog("WARNING", message);
    }

    /// <summary>
    /// Записать ошибку
    /// </summary>
    public void LogError(string message, Exception? ex = null)
    {
        var fullMessage = ex != null 
            ? $"{message}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}"
            : message;
        WriteLog("ERROR", fullMessage);
    }

    private void WriteLog(string level, string message)
    {
        lock (_lockObject)
        {
            try
            {
                // Ротация: если лог превысил лимит, переименовываем в .old
                RotateIfNeeded();

                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Игнорируем ошибки записи в лог
            }
        }
    }

    /// <summary>
    /// Ротация лог-файла при превышении MaxLogSize.
    /// Старый файл переименовывается в PAS.log.old (предыдущий .old удаляется).
    /// </summary>
    private void RotateIfNeeded()
    {
        try
        {
            var fi = new FileInfo(_logFilePath);
            if (fi.Exists && fi.Length > MaxLogSize)
            {
                var backup = _logFilePath + ".old";
                if (File.Exists(backup))
                    File.Delete(backup);
                File.Move(_logFilePath, backup);
            }
        }
        catch
        {
            // Ротация не критична — если не удалась, продолжаем писать
        }
    }

    /// <summary>
    /// Получить путь к файлу лога
    /// </summary>
    public string GetLogFilePath() => _logFilePath;
}
