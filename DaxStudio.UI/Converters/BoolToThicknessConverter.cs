using System;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    class BoolToThicknessConverter:IValueConverter
    {
        
            public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                var flag = value as bool?;
                if (flag == null) return Binding.DoNothing;
                if (flag == false) return Binding.DoNothing;
                var param = System.Convert.ToString(parameter);
                var paramArray = param.Split(' ');
                if (paramArray.Length != 4) return Binding.DoNothing;
                var thickness = new Thickness(System.Convert.ToDouble(paramArray[0])
                    , System.Convert.ToDouble(paramArray[1])
                    , System.Convert.ToDouble(paramArray[2])
                    , System.Convert.ToDouble(paramArray[3]));
                return thickness;

            }

            public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                return null;
            }
        
    }
}
