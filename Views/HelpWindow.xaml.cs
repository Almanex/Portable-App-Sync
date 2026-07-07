using System.Windows;
using PAS.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace PAS.Views;

/// <summary>
/// Окно справки
/// </summary>
public partial class HelpWindow : FluentWindow
{
    public HelpWindow()
    {
        SystemThemeWatcher.Watch(this, WindowBackdropType.None);
        InitializeComponent();
        // LocalizationManager — синглтон с INotifyPropertyChanged.
        // Устанавливаем как DataContext, чтобы все {Binding} в XAML
        // автоматически подхватывали текущий язык без лишних Source=...
        DataContext = LocalizationManager.Instance;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
