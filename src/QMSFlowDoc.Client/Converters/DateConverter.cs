using Microsoft.UI.Xaml.Data;
using System;

namespace QMSFlowDoc.Client.Converters;

public class DateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime dt)
        {
            return dt.ToLocalTime().ToString("d");
        }
        if (value is DateTimeOffset dto)
        {
            return dto.ToLocalTime().ToString("d");
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
