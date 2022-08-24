using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace DaxStudio.UI.Converters
{
    [ContentProperty("Items")]
    class ResourceThemeLookupConverter: IMultiValueConverter
    {
        public ResourceDictionary Items { get; set; }



        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == null) return null;
            if (values.Length != 2) return null;

            string key = values[0] as string;
            string theme = values[1] as string;

            if (key == null)
            {
                if (values[0].GetType().IsEnum)
                    key = Enum.GetName(values[0].GetType(), values[0]);
            }

            if (key == null)
                return Binding.DoNothing;

            var itm = Items[$"{theme}.{key}"];
            return itm;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return (object[])Binding.DoNothing;
        }
    }

}
