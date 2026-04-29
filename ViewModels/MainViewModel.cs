using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using PAS.Models;
using PAS.Services;
using Microsoft.Win32;

namespace PAS.ViewModels;

/// <summary>
/// ViewModel главного окна
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly SystemScanService _scanService;
    private readonly ScriptGeneratorService _scriptGenerator;
    private readonly DownloadService _downloadService;
    private readonly LoggingService _logger;
    private readonly ICollectionView _applicationsView;
    private CancellationTokenSource? _descriptionTokenSource;
    private CancellationTokenSource? _exportCts;

    private ExportMode _selectedMode = ExportMode.OnlineScript;
    private bool _isScanning;
    private bool _isExporting;
    private double _progressValue;
    private string _progressText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedAppFilter = AppListFilter.All;
    private string _lastExportSummary = string.Empty;
    private string _lastExportFolder = string.Empty;
    private ExportResult? _lastExportResult;
    private int _lastExcludedCount;

    public MainViewModel(
        SystemScanService scanService,
        ScriptGeneratorService scriptGenerator,
        DownloadService downloadService,
        LoggingService logger)
    {
        _scanService = scanService;
        _scriptGenerator = scriptGenerator;
        _downloadService = downloadService;
        _logger = logger;

        // Настройка фильтрации (ICollectionView)
        _applicationsView = CollectionViewSource.GetDefaultView(Applications);
        _applicationsView.Filter = FilterApplications;

        // Инициализация команд
        ScanCommand = new AsyncRelayCommand(async _ => await ScanSystemAsync(true), _ => !IsScanning);
        ExportCommand = new AsyncRelayCommand(async _ => await ExportAsync(), _ => !IsExporting && CastFiltered().Any(a => a.IsSelected));
        CancelExportCommand = new RelayCommand(_ => CancelExport(), _ => IsExporting);
        SelectAllCommand = new RelayCommand(_ => SelectAll());
        UnselectAllCommand = new RelayCommand(_ => UnselectAll());
        OpenExportFolderCommand = new RelayCommand(_ => OpenLastExportFolder(), _ => !string.IsNullOrWhiteSpace(LastExportFolder) && Directory.Exists(LastExportFolder));
        ShowHelpCommand = new RelayCommand(_ => ShowHelp());
        ShowAboutCommand = new RelayCommand(_ => ShowAbout());

        // Подписка на изменение языка для обновления всех привязок в UI
        LocalizationManager.Instance.PropertyChanged += (s, e) => {
            OnPropertyChanged(string.Empty);
            if (_lastExportResult is not null)
            {
                LastExportSummary = BuildExportSummary(_lastExportResult, _lastExcludedCount);
            }
            _applicationsView.Refresh(); // Обновить фильтр, если он зависит от локализованных данных (в будущем)
        };

        // Автоматическое сканирование при запуске
        _ = ScanSystemAsync();
    }

    private void ShowHelp()
    {
        var helpWindow = new Views.HelpWindow();
        helpWindow.ShowDialog();
    }

    private void ShowAbout()
    {
        var aboutWindow = new Views.AboutWindow();
        aboutWindow.ShowDialog();
    }

    #region Properties

    public ObservableCollection<InstalledApp> Applications { get; } = new();

    private bool _showAllApps = false;
    public bool ShowAllApps
    {
        get => _showAllApps;
        set
        {
            if (_showAllApps != value)
            {
                _showAllApps = value;
                OnPropertyChanged();
                _applicationsView.Refresh();
            }
        }
    }

    public ICollectionView FilteredApplications => _applicationsView;

    private bool FilterApplications(object item)
    {
        if (item is not InstalledApp app) return false;

        // Фильтр по системным компонентам
        if (SelectedAppFilter != AppListFilter.Excluded && !ShowAllApps && (app.IsSystemComponent || app.IsStoreApp))
        {
            return false;
        }

        if (SelectedAppFilter == AppListFilter.OfflineReady && (app.IsExcludedFromExport || !app.SupportsOfflineDownload))
        {
            return false;
        }

        if (SelectedAppFilter == AppListFilter.OnlineFallback && (app.IsExcludedFromExport || app.SupportsOfflineDownload))
        {
            return false;
        }

        if (SelectedAppFilter == AppListFilter.Excluded && !app.IsExcludedFromExport)
        {
            return false;
        }

        // Фильтр по поиску
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            return app.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   app.WingetId.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private IEnumerable<InstalledApp> CastFiltered() => _applicationsView.Cast<InstalledApp>();

    public string SelectedCount
    {
        get => Applications.Where(a => a.IsSelected).Count().ToString();
    }

    public string SelectedCountText
    {
        get
        {
            var count = Applications.Where(a => a.IsSelected).Count();
            return LocalizationManager.Instance.Format("SelectedCountFormat", count);
        }
    }

    private void SelectAll()
    {
        foreach (var app in CastFiltered())
        {
            app.IsSelected = true;
        }
    }

    private void UnselectAll()
    {
        foreach (var app in CastFiltered())
        {
            app.IsSelected = false;
        }
    }



    public ExportMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (_selectedMode != value)
            {
                _selectedMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExportButtonText));
                OnPropertyChanged(nameof(IsProgressVisible));
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (_isScanning != value)
            {
                _isScanning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProgressVisible));
                ((AsyncRelayCommand)ScanCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsExporting
    {
        get => _isExporting;
        set
        {
            if (_isExporting != value)
            {
                _isExporting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProgressVisible));
                ((AsyncRelayCommand)ExportCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            if (Math.Abs(_progressValue - value) > 0.01)
            {
                _progressValue = value;
                OnPropertyChanged();
            }
        }
    }

    public string ProgressText
    {
        get => _progressText;
        set
        {
            if (_progressText != value)
            {
                _progressText = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                _applicationsView.Refresh();
            }
        }
    }

    public string SelectedAppFilter
    {
        get => _selectedAppFilter;
        set
        {
            if (_selectedAppFilter != value)
            {
                _selectedAppFilter = value;
                OnPropertyChanged();
                _applicationsView.Refresh();
            }
        }
    }

    public string LastExportSummary
    {
        get => _lastExportSummary;
        set
        {
            if (_lastExportSummary != value)
            {
                _lastExportSummary = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLastExportSummary));
            }
        }
    }

    public bool HasLastExportSummary => !string.IsNullOrWhiteSpace(LastExportSummary);

    public string LastExportFolder
    {
        get => _lastExportFolder;
        set
        {
            if (_lastExportFolder != value)
            {
                _lastExportFolder = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanOpenExportFolder));
                ((RelayCommand)OpenExportFolderCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanOpenExportFolder => !string.IsNullOrWhiteSpace(LastExportFolder) && Directory.Exists(LastExportFolder);

    public string ExportButtonText => SelectedMode == ExportMode.OnlineScript
        ? LocalizationManager.Instance.Get("ExportButtonOnline")
        : LocalizationManager.Instance.Get("ExportButtonOffline");

    public bool IsProgressVisible => (SelectedMode == ExportMode.OfflinePackage && IsExporting) || IsScanning;



    #endregion

    #region Commands

    public ICommand ScanCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand CancelExportCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand UnselectAllCommand { get; }
    public ICommand OpenExportFolderCommand { get; }
    public ICommand ShowHelpCommand { get; }
    public ICommand ShowAboutCommand { get; }

    #endregion

    #region Methods

    private void OnAppSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstalledApp.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedCountText));
        }
    }

    private async Task ScanSystemAsync(bool forceRefresh = false)
    {
        _descriptionTokenSource?.Cancel();

        if (forceRefresh)
        {
            _scanService.ClearCache();
        }

        IsScanning = true;
        StatusMessage = LocalizationManager.Instance.Get("StatusScanning");
        Applications.Clear();

        try
        {
            // SystemScanService уже проверяет доступность winget внутри ScanSystem(),
            // поэтому повторная проверка здесь не нужна.
            var apps = await _scanService.QuickScan();

            if (apps.Count == 0)
            {
                // Возможно winget недоступен — проверяем для показа сообщения
                var L = LocalizationManager.Instance;
                var wingetAvailable = await _scanService.CheckWingetAvailability();
                if (!wingetAvailable)
                {
                    StatusMessage = L.Get("StatusWingetNotFound");
                    MessageBox.Show(
                        L.Get("WingetMissingBody"),
                        L.Get("WingetMissingTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    _logger.LogError("Winget не найден. Сканирование невозможно.");
                    return;
                }
            }

            int index = 1;
            var l = LocalizationManager.Instance;
            foreach (var app in apps)
            {
                app.Index = index++;
                // Инициализируем локализованные статусы (п. 10, 11)
                app.Status = app.IsExcludedFromExport ? l.Get("AppStatusExcluded") : l.Get("AppStatusReady");
                app.Description = app.IsExcludedFromExport && !string.IsNullOrWhiteSpace(app.ExclusionReason)
                    ? app.ExclusionReason
                    : l.Get("DescriptionLoading");
                
                app.PropertyChanged += OnAppSelectionChanged;
                Applications.Add(app);
            }

            StatusMessage = LocalizationManager.Instance.Format("StatusAppsFound", Applications.Count);
            _logger.LogInfo($"Сканирование завершено. Приложений: {Applications.Count}");
            
            // Обновляем отфильтрованный список
            OnPropertyChanged(nameof(FilteredApplications));
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedCountText));

            // Запускаем фоновую загрузку описаний
            _descriptionTokenSource = new CancellationTokenSource();
            _ = LoadDescriptionsInBackground(_descriptionTokenSource.Token);
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationManager.Instance.Get("StatusExportError");
            MessageBox.Show($"{LocalizationManager.Instance.Get("StatusExportError")}: {ex.Message}",
                LocalizationManager.Instance.Get("ExportErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            _logger.LogError("Ошибка сканирования", ex);
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task ExportAsync()
    {
        var l = LocalizationManager.Instance;
        // Экспортируем только то, что видно в текущем фильтре и выбрано (п. 9)
        var selectedApps = CastFiltered().Where(a => a.IsSelected).ToList();
        var excludedSelectedApps = selectedApps.Where(a => a.IsExcludedFromExport).ToList();
        selectedApps = selectedApps.Where(a => !a.IsExcludedFromExport).ToList();

        if (!selectedApps.Any())
        {
            var warning = excludedSelectedApps.Count > 0
                ? l.Format("ExportOnlyExcludedSelected", excludedSelectedApps.Count)
                : l.Get("ExportSelectOne");
            MessageBox.Show(warning, l.Get("ExportWarning"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Создаём токен отмены для текущего экспорта
        _exportCts?.Cancel();
        _exportCts = new CancellationTokenSource();
        var ct = _exportCts.Token;

        try
        {
            ExportResult result;

            if (SelectedMode == ExportMode.OnlineScript)
            {
                // Показываем диалог ДО установки IsExporting
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Batch Script (*.bat)|*.bat|PowerShell Script (*.ps1)|*.ps1",
                    FileName = "RestoreApps.bat",
                    Title = l.Get("ExportSaveScriptTitle")
                };

                if (saveDialog.ShowDialog() != true) return;

                IsExporting = true;
                StatusMessage = l.Get("StatusCreatingScript");

                var extension = Path.GetExtension(saveDialog.FileName).ToLower();

                if (extension == ".ps1")
                    result = await _scriptGenerator.GeneratePowerShellScript(selectedApps, saveDialog.FileName);
                else
                    result = await _scriptGenerator.GenerateBatchScript(selectedApps, saveDialog.FileName);
            }
            else
            {
                // Показываем диалог ДО установки IsExporting
                var folderDialog = new OpenFolderDialog
                {
                    Title = l.Get("ExportSelectFolder")
                };

                if (folderDialog.ShowDialog() != true) return;

                IsExporting = true;
                StatusMessage = l.Get("StatusDownloading");

                var progress = new Progress<DownloadProgress>(p =>
                {
                    ProgressValue = p.OverallProgress;
                    ProgressText = l.Format("ExportDownloadProgress", p.CurrentAppIndex, p.TotalApps, p.CurrentAppName);
                    StatusMessage = ProgressText;
                });

                result = await _downloadService.DownloadPackages(selectedApps, folderDialog.FolderName, progress, ct);
            }

            // Если операция была отменена, не показываем результат
            if (ct.IsCancellationRequested)
            {
                StatusMessage = l.Get("ExportCancelByUser");
                return;
            }

            // Показываем результат
            if (result.Success)
            {
                LastExportFolder = ResolveExportFolder(result.OutputPath);
                _lastExportResult = result;
                _lastExcludedCount = excludedSelectedApps.Count;
                LastExportSummary = BuildExportSummary(result, excludedSelectedApps.Count);

                var message = LastExportSummary;
                if (result.SkippedCount > 0)
                {
                    message += $"\n\n{l.Format("ExportSummarySkipped", result.SkippedCount)}";
                    message += $"\n{string.Join("\n", result.SkippedApps)}";
                }
                if (result.ErrorCount > 0)
                {
                    message += $"\n\n{l.Format("ExportFailedToProcess", result.ErrorCount)}";
                    message += $"\n{string.Join("\n", result.FailedApps)}";
                }
                if (excludedSelectedApps.Count > 0)
                {
                    message += $"\n\n{l.Format("ExportExcludedIgnored", excludedSelectedApps.Count)}";
                    message += $"\n{string.Join("\n", excludedSelectedApps.Select(a => $"- {a.Name}"))}";
                }

                MessageBox.Show(message, l.Get("ExportSuccess"), MessageBoxButton.OK, MessageBoxImage.Information);
                StatusMessage = l.Get("ExportDoneStatus");
            }
            else
            {
                MessageBox.Show(result.Message, l.Get("StatusExportError"), MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = l.Get("StatusExportError");
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = l.Get("ExportCancelByUser");
            _logger.LogInfo("Экспорт отменён пользователем");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{l.Get("StatusExportError")}: {ex.Message}", l.Get("StatusExportError"), MessageBoxButton.OK, MessageBoxImage.Error);
            _logger.LogError("Ошибка экспорта", ex);
            StatusMessage = l.Get("StatusExportError");
        }
        finally
        {
            IsExporting = false;
            ProgressValue = 0;
            ProgressText = string.Empty;
        }
    }

    private string BuildExportSummary(ExportResult result, int excludedCount)
    {
        var l = LocalizationManager.Instance;
        var lines = new List<string>
        {
            result.Message,
            l.Format("ExportSummarySuccess", result.SuccessCount),
            l.Format("ExportSummaryFallback", result.FallbackCount),
            l.Format("ExportSummarySkipped", result.SkippedCount),
            l.Format("ExportSummaryErrors", result.ErrorCount)
        };

        if (excludedCount > 0)
        {
            lines.Add(l.Format("ExportSummaryExcluded", excludedCount));
        }

        if (!string.IsNullOrWhiteSpace(result.OnlineFallbackScriptPath))
        {
            lines.Add(l.Format("ExportSummaryFallbackPath", result.OnlineFallbackScriptPath));
        }

        if (!string.IsNullOrWhiteSpace(result.OutputPath))
        {
            lines.Add(l.Format("ExportSummaryOutputPath", result.OutputPath));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string ResolveExportFolder(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return string.Empty;
        }

        return Directory.Exists(outputPath)
            ? outputPath
            : Path.GetDirectoryName(outputPath) ?? string.Empty;
    }

    private void OpenLastExportFolder()
    {
        if (!Directory.Exists(LastExportFolder))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = LastExportFolder,
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Отмена текущей операции экспорта
    /// </summary>
    private void CancelExport()
    {
        _exportCts?.Cancel();
        _logger.LogInfo("Пользователь запросил отмену экспорта");
    }

    private async Task LoadDescriptionsInBackground(CancellationToken token)
    {
        // Снимаем копию списка в UI-потоке для безопасности
        var apps = Applications.ToList();
        var l = LocalizationManager.Instance;
        var loadingText = l.Get("DescriptionLoading");
        
        foreach (var app in apps)
        {
            if (token.IsCancellationRequested)
                break;

            if (app.IsExcludedFromExport)
                continue;

            // Проверяем на пустоту или на локализованную строку загрузки
            if (string.IsNullOrEmpty(app.Description) || app.Description == loadingText)
            {
                var appInfo = await _scanService.GetAppInfo(app.WingetId);
                if (!token.IsCancellationRequested)
                {
                    // Обновляем свойства через Dispatcher для потокобезопасности
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        app.Description = !string.IsNullOrEmpty(appInfo.Description) 
                            ? appInfo.Description 
                            : l.Get("DescriptionUnavailable");
                        
                        // Если winget show смог получить адекватное красивое имя, перезаписываем ID
                        if (!string.IsNullOrEmpty(appInfo.Name) && appInfo.Name != app.WingetId)
                        {
                            app.Name = appInfo.Name;
                        }
                    });
                }
            }
        }
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
