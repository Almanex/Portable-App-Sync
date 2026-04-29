namespace PAS.Models;

/// <summary>
/// Режимы экспорта приложений
/// </summary>
public enum ExportMode
{
    /// <summary>
    /// Создание скрипта автоустановки (онлайн)
    /// </summary>
    OnlineScript,
    
    /// <summary>
    /// Загрузка дистрибутивов (оффлайн)
    /// </summary>
    OfflinePackage
}
