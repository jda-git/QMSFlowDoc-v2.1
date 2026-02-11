using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace QMSFlowDoc.Client.Converters;

public class DateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime dt)
        {
            if (dt == DateTime.MinValue) return "";
            return dt.ToLocalTime().ToString("d");
        }
        if (value is DateTimeOffset dto)
        {
            if (dto == DateTimeOffset.MinValue) return "";
            return dto.ToLocalTime().ToString("d");
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}
