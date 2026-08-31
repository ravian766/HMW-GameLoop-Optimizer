using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GameLoopOptimizer;

public class BooleanToRunningStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? "GameLoop: Running" : "GameLoop: Inactive";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BooleanToStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? "Active (In Foreground)" : "Standby / Inactive";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return value.ToString()!.Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            return Enum.Parse(targetType, parameter.ToString()!);
        }
        return Binding.DoNothing;
    }
}

public class StringToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return value.ToString()!.Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            return parameter.ToString()!;
        }
        return Binding.DoNothing;
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : true;
    }
}

public class BooleanToStatusBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush GreenBrush = new(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly System.Windows.Media.SolidColorBrush SlateBrush = new(System.Windows.Media.Color.FromRgb(0x64, 0x74, 0x8B));
    private static readonly System.Windows.Media.SolidColorBrush RedBrush = new(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isTrue = value is bool b && b;
        if (isTrue)
        {
            return GreenBrush;
        }

        if (parameter is string p && p.Equals("danger", StringComparison.OrdinalIgnoreCase))
        {
            return RedBrush;
        }

        return SlateBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BooleanToAdminBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush EmeraldBrush = new(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly System.Windows.Media.SolidColorBrush MutedBrush = new(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? EmeraldBrush : MutedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

