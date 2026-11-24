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

/// <summary>
/// Shows an element when its boolean binding is FALSE (true → Collapsed).
///
/// WPF's built-in BooleanToVisibilityConverter ignores ConverterParameter entirely, so the
/// "no save files found" banner that bound
/// <c>Converter={StaticResource BooleanToVisibilityConverter}, ConverterParameter=Collapsed</c>
/// was actually VISIBLE when files WERE found - the exact opposite of its text. That is how the
/// tab showed a populated dropdown alongside "No save files were found". Use this converter for
/// any show-on-empty state; the MainWindow admin banner had the same inverted-visibility bug.
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BooleanToStatusBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush FallbackGreen = new(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly System.Windows.Media.SolidColorBrush FallbackSlate = new(System.Windows.Media.Color.FromRgb(0x64, 0x74, 0x8B));
    private static readonly System.Windows.Media.SolidColorBrush FallbackRed = new(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isTrue = value is bool b && b;
        if (isTrue)
        {
            return Application.Current?.TryFindResource("BrushAccentGreen") as System.Windows.Media.Brush ?? FallbackGreen;
        }

        if (parameter is string p && p.Equals("danger", StringComparison.OrdinalIgnoreCase))
        {
            return Application.Current?.TryFindResource("BrushAccentRed") as System.Windows.Media.Brush ?? FallbackRed;
        }

        return Application.Current?.TryFindResource("BrushTextMuted") as System.Windows.Media.Brush ?? FallbackSlate;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BooleanToAdminBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush FallbackEmerald = new(System.Windows.Media.Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly System.Windows.Media.SolidColorBrush FallbackMuted = new(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isTrue = value is bool b && b;
        return isTrue
            ? (Application.Current?.TryFindResource("BrushAccentGreen") as System.Windows.Media.Brush ?? FallbackEmerald)
            : (Application.Current?.TryFindResource("BrushTextMuted") as System.Windows.Media.Brush ?? FallbackMuted);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BooleanToStatusIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? "✓" : "✕";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class IntGreaterThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i) return i > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (value is long l) return l > 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BooleanToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string[] parts = (parameter as string ?? "True|False").Split('|');
        string trueText = parts[0];
        string falseText = parts.Length > 1 ? parts[1] : string.Empty;
        return (value is bool b && b) ? trueText : falseText;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

