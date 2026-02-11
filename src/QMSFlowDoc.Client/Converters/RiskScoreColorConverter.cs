using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace QMSFlowDoc.Client.Converters;

public class RiskScoreColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int score)
        {
            if (score >= 20) return new SolidColorBrush(Microsoft.UI.Colors.Red);
            if (score >= 12) return new SolidColorBrush(Microsoft.UI.Colors.Orange);
            if (score >= 6) return new SolidColorBrush(Microsoft.UI.Colors.Yellow);
            return new SolidColorBrush(Microsoft.UI.Colors.Green);
        }
        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}
