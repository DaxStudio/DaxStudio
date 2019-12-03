using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class HotKeyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Hotkey hotkey = new Hotkey( (string)value);
            // TODO - get key and modifiers from value
            return hotkey;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Hotkey key = value as Hotkey;
            return key?.ToString()??string.Empty;
        }
    }
}
