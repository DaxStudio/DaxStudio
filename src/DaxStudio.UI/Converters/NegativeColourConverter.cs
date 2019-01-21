using System;
using System.Windows.Media;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class NegativeColourConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int val;
            int.TryParse(value.ToString(),out val);
            Color col;
            string colStr = parameter.ToString();
            byte a,r,g,b;
            
            if (colStr.StartsWith("#"))
            {
                switch (colStr.Length)
                {
                    case 7: 
                        a = 255;
                        r = System.Convert.ToByte( colStr.Substring(1,2),16);
                        g = System.Convert.ToByte( colStr.Substring(3,2),16);
                        b = System.Convert.ToByte( colStr.Substring(5,2),16);
                        break;
                    case 9:
                        a = System.Convert.ToByte( colStr.Substring(1,2),16);
                        r = System.Convert.ToByte( colStr.Substring(3,2),16);
                        g = System.Convert.ToByte( colStr.Substring(5,2),16);
                        b = System.Convert.ToByte( colStr.Substring(7,2),16);
                        break;
                    default:
                        return Binding.DoNothing;
                }
                col = Color.FromArgb(a,r,g,b);
            }
            else {
                col = (Color)ColorConverter.ConvertFromString(colStr);
            }

            if (val < 0 && col != null) return new System.Windows.Media.SolidColorBrush(col);
            
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
