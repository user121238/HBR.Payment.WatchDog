using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HBR.Payment.WatchDog;

public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var statusText = value as string ?? string.Empty;
        var part = parameter as string ?? "Fill";

        var state = GetState(statusText);
        return part switch
        {
            "Border" => GetBorderBrush(state),
            "Foreground" => GetForegroundBrush(state),
            _ => GetFillBrush(state)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string GetState(string statusText)
    {
        if (statusText.StartsWith("运行中", StringComparison.Ordinal))
        {
            return "running";
        }

        if (statusText.StartsWith("已手动停止", StringComparison.Ordinal))
        {
            return "manual";
        }

        if (statusText.StartsWith("已禁用", StringComparison.Ordinal))
        {
            return "disabled";
        }

        if (statusText.Contains("不存在", StringComparison.Ordinal) || statusText.Contains("为空", StringComparison.Ordinal))
        {
            return "error";
        }

        return "stopped";
    }

    private static System.Windows.Media.Brush GetFillBrush(string state)
    {
        return state switch
        {
            "running" => CreateBrush("#FFF3F3F3"),
            "manual" => CreateBrush("#FFF8F5F5"),
            "disabled" => CreateBrush("#FFF6F6F6"),
            "error" => CreateBrush("#FFFAF4F4"),
            _ => CreateBrush("#FFF7F7F7")
        };
    }

    private static System.Windows.Media.Brush GetBorderBrush(string state)
    {
        return state switch
        {
            "running" => CreateBrush("#FFE0E0E0"),
            "manual" => CreateBrush("#FFE8DCDC"),
            "disabled" => CreateBrush("#FFE3E3E3"),
            "error" => CreateBrush("#FFE8D8D8"),
            _ => CreateBrush("#FFE4E4E4")
        };
    }

    private static System.Windows.Media.Brush GetForegroundBrush(string state)
    {
        return state switch
        {
            "running" => CreateBrush("#FF161616"),
            "manual" => CreateBrush("#FF6F5454"),
            "disabled" => CreateBrush("#FF7A7A7A"),
            "error" => CreateBrush("#FF7D4F4F"),
            _ => CreateBrush("#FF666666")
        };
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
