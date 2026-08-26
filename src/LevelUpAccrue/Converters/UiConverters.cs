using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace LevelUpAccrue.Converters;

public sealed class DeltaBrushConverter : IValueConverter
{
    private static readonly Brush Positive = new SolidColorBrush(Color.FromRgb(23, 107, 74));
    private static readonly Brush Negative = new SolidColorBrush(Color.FromRgb(179, 58, 58));
    private static readonly Brush Zero = new SolidColorBrush(Color.FromRgb(102, 114, 127));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var amount = value is decimal number ? number : 0m;
        return amount > 0 ? Positive : amount < 0 ? Negative : Zero;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BoolStatusBrushConverter : IValueConverter
{
    private static readonly Brush Paid = new SolidColorBrush(Color.FromRgb(23, 107, 74));
    private static readonly Brush Pending = new SolidColorBrush(Color.FromRgb(185, 106, 19));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Paid : Pending;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class PositiveCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
