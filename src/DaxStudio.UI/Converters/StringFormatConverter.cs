using ADOTabular;
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
                switch ((string)values[2].ToString().ToLower()) //DataTypeName
                {
                    case "datetime":
                        DateTime.TryParse((string)values[0],out DateTime outVal );
                        return string.Format(string.Format("{{0:{0}}}", (string)values[1]), outVal);
                        
                    case "string":
                        val = (string)values[0];
                        break;
                    case "int":
                    case "int16":
                    case "int32":
                    case "int64":
                        val = long.TryParse((string)values[0],out long longVal);
                        return (bool)val ? string.Format(string.Format("{{0:{0}}}", (string)values[1]), longVal): Common.Constants.Err;
                    case "decimal":
                        val = decimal.TryParse((string)values[0], out Decimal decVal);
                        return (bool)val ? string.Format(string.Format("{{0:{0}}}", (string)values[1]), decVal): Common.Constants.Err;
                    case "double":
                        val = double.TryParse((string)values[0], out double dblVal);
                        return (bool)val?string.Format(string.Format("{{0:{0}}}", (string)values[1]), dblVal): Common.Constants.Err;
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
