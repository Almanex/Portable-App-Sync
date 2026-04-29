using System.Windows;
using PAS.Services;

namespace PAS.Views;

/// <summary>
/// Окно справки
/// </summary>
public partial class HelpWindow : Window
{
    public HelpWindow()
    {
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
