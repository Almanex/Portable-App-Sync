using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using PAS.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace PAS.Views;

public partial class AboutWindow : FluentWindow
{
    public AboutWindow()
    {
        SystemThemeWatcher.Watch(this, WindowBackdropType.None);
        InitializeComponent();
        DataContext = LocalizationManager.Instance;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }
}
