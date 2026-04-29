using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PAS.Models;

namespace PAS.Converters;

/// <summary>
/// Конвертер Enum в Boolean для RadioButton
/// </summary>
public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            return Enum.Parse(typeof(ExportMode), parameter.ToString()!);
        }

        return Binding.DoNothing;
    }
}

/// <summary>
/// Конвертер Boolean в Visibility
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }

        return false;
    }
}
