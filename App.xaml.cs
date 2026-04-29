using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PAS.Services;
using PAS.ViewModels;
using PAS.Views;

namespace PAS;

public partial class App : Application
{
    private static IServiceProvider? _serviceProvider;
    private static string? _logPath;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Настройка исключений
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Конфигурация DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Сохраняем путь для логов (для фатальных ошибок)
        _logPath = _serviceProvider.GetRequiredService<AppPaths>().LogFilePath;

        // Запуск главного окна
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<AppPaths>();
        services.AddSingleton<LoggingService>();
        services.AddSingleton<CacheService>();
        
        // Winget Services
        services.AddSingleton<WingetService>();
        services.AddSingleton<OfflineCompatibilityService>();
        services.AddSingleton<SystemScanService>();
        services.AddSingleton<ScriptGeneratorService>();
        services.AddSingleton<DownloadService>();

        // UI
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("UI Thread", e.Exception);
        MessageBox.Show(
            $"Произошла непредвиденная ошибка:\n\n{e.Exception.Message}\n\nПриложение будет закрыто.",
            "Ошибка",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
        e.Handled = true;
        Environment.Exit(1);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException("Background Thread", ex);
        }
        if (e.IsTerminating)
        {
            Environment.Exit(1);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException("Unobserved Task", e.Exception);
        e.SetObserved();
    }

    private static void LogException(string source, Exception ex)
    {
        try
        {
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [FATAL] [{source}] {ex.Message}\n{ex.StackTrace}\n";
            var path = _logPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PAS", "PAS.log");
            File.AppendAllText(path, entry, Encoding.UTF8);
        }
        catch
        {
            // Игнорируем ошибки записи лога при падении
        }
    }
}
