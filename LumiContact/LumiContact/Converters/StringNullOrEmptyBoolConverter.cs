using System;
using System.Globalization;
using Xamarin.Forms;

namespace LumiContact.Converters
{
    public class StringNullOrEmptyBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            var isNullOrEmpty = string.IsNullOrEmpty(str);

            if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                return !isNullOrEmpty;
            }

            return isNullOrEmpty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}