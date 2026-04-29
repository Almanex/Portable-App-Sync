using System.Windows;
using System.Windows.Controls;
using PAS.Services;

namespace PAS.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
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
}