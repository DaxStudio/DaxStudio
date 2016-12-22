using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    
    public class StringFormatConverter : IMultiValueConverter
    {


        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue) return string.Empty;
            if ((string)values[0] == string.Empty) return string.Empty;
            if (values[1] == DependencyProperty.UnsetValue) return values[0].ToString();
            object val = null;
            switch ((string)values[2]) //DataTypeName
            {
                case "DateTime":
                    val = DateTime.Parse((string)values[0]);
                    break;
                case "String":
                    val = (string)values[0];
                    break;
                case "Int64":
                    val = long.Parse((string)values[0]);
                    break;
                case "Decimal":
                    val = decimal.Parse((string)values[0]);
                    break;
            }
            return string.Format(string.Format("{{0:{0}}}",(string)values[1]), val);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing};
        }
    }
}
