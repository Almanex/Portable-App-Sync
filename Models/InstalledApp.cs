using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PAS.Models;

/// <summary>
/// Модель установленного приложения
/// </summary>
public class InstalledApp : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _status = string.Empty;

    /// <summary>
    /// Порядковый номер приложения
    /// </summary>
    public int Index { get; set; }

    private string _description = "Загрузка описания...";
    /// <summary>
    /// Описание приложения (загружается в фоне)
    /// </summary>
    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged();
            }
        }
    }

    private string _name = string.Empty;
    /// <summary>
    /// Название приложения
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// ID пакета в Winget (например, "Google.Chrome")
    /// </summary>
    public string WingetId { get; set; } = string.Empty;

    /// <summary>
    /// Версия приложения
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Доступность в репозитории Winget
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Выбрано пользователем для экспорта
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Текущий статус (Готово, Загрузка, Ошибка и т.д.)
    /// </summary>
    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Источник установки (winget, msstore и т.д.)
    /// </summary>
    public string Source { get; set; } = "winget";

    /// <summary>
    /// Является ли приложение Store-пакетом (проверено через winget show)
    /// </summary>
    public bool IsStoreApp { get; set; } = false;

    /// <summary>
    /// Является ли приложение системным компонентом или библиотекой
    /// </summary>
    public bool IsSystemComponent { get; set; } = false;

    /// <summary>
    /// Поддерживает ли приложение оффлайн-загрузку через winget download
    /// </summary>
    public bool SupportsOfflineDownload { get; set; } = true;

    /// <summary>
    /// Служебные компоненты исключаются из экспорта по умолчанию, но могут быть показаны в списке.
    /// </summary>
    public bool IsExcludedFromExport { get; set; } = false;

    /// <summary>
    /// Короткое объяснение, почему приложение исключено из обычного экспорта.
    /// </summary>
    public string ExclusionReason { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
