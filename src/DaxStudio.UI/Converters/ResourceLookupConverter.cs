using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace DaxStudio.UI.Converters
{
    [ContentProperty("Items")]
    class ResourceLookupConverter: IValueConverter
    {
        public ResourceDictionary Items { get; set; }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return null;

            string key;
            key = value as string;
            if (key == null)
            {
                if (value.GetType().IsEnum)
                    key = Enum.GetName(value.GetType(), value);
            }
            
            if (key == null)
                return Binding.DoNothing;
            
            var itm = Items[key];
            return itm;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

}
