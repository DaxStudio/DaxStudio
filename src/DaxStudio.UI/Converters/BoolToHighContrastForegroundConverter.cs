using ICSharpCode.NRefactory.Ast;
using Microsoft.Identity.Client.NativeInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DaxStudio.UI.Converters
{
    public class BoolToHighContrastForegroundConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Boolean && !(bool)value & parameter is string & !string.IsNullOrEmpty((string)parameter))
            {
                return (Color)ColorConverter.ConvertFromString((string)parameter);
            }
            // 
            return SystemColors.WindowTextColor;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Color color && color == SystemColors.WindowTextColor)
            {
                return true;
            }
            return false;
        }
    }
}
