using System;
using System.Globalization;
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
            if (values[0] != null)
            {
                switch ((string)values[2]) //DataTypeName
                {
                    case "DateTime":
                        DateTime.TryParse((string)values[0],out DateTime outVal );
                        return string.Format(string.Format("{{0:{0}}}", (string)values[1]), outVal);
                        
                    case "String":
                        val = (string)values[0];
                        break;
                    case "Int64":
                        val = long.TryParse((string)values[0],out long longVal);
                        return string.Format(string.Format("{{0:{0}}}", (string)values[1]), longVal);
                        
                    case "Decimal":
                        val = decimal.TryParse((string)values[0], out Decimal decVal);
                        return string.Format(string.Format("{{0:{0}}}", (string)values[1]), decVal);
                }
            }
            return string.Format(string.Format("{{0:{0}}}",(string)values[1]), val);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing};
        }
    }
}
