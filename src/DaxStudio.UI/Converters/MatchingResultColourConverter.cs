using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace DaxStudio.UI.Converters
{
    public class MatchingResultColourConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string val;
            val = value as string;

            if (value == null) return Binding.DoNothing;

            switch (val)
            {
                case "matchFound":
                    return new SolidColorBrush(Colors.Green);
                case "attemptedFailed":
                    return new SolidColorBrush(Colors.Red);
                default:
                    return Binding.DoNothing;
            }

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
