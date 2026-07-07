using System.Windows;
using System.Windows.Controls;
using PAS.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace PAS.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        // Watch system theme changes (Light/Dark mode sync) without backdrop transparency
        SystemThemeWatcher.Watch(this, WindowBackdropType.None);
        InitializeComponent();
    }

    /// <summary>
    /// Динамическая нумерация строк по их реальной позиции в отображаемом списке.
    /// Обновляется автоматически при любой фильтрации или сортировке.
    /// </summary>
    private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        e.Row.Header = (e.Row.GetIndex() + 1).ToString();
    }

    /// <summary>
    /// Переключение языка интерфейса через ComboBox.
    /// </summary>
    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            LocalizationManager.Instance.CurrentLanguage = lang;
        }
    }

    /// <summary>
    /// Переключение темы интерфейса (Системная / Светлая / Темная).
    /// </summary>
    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo == null || ThemeCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string themeTag)
        {
            return;
        }

        switch (themeTag)
        {
            case "system":
                SystemThemeWatcher.Watch(this, WindowBackdropType.None);
                ApplicationThemeManager.ApplySystemTheme();
                break;
            case "light":
                SystemThemeWatcher.UnWatch(this);
                ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.None);
                break;
            case "dark":
                SystemThemeWatcher.UnWatch(this);
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.None);
                break;
        }
    }
}