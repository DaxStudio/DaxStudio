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
    public class RightMarginMultiplyConverter: IMultiValueConverter
    {

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            long width = 0;
            double multiple = 0.0;

            try
            {
                width = System.Convert.ToInt64(values[0]);
                multiple = System.Convert.ToDouble(values[1]);
            }
            catch
            {
                return Binding.DoNothing;
            }

            var param = System.Convert.ToString(parameter);
            var paramArray = param.Split(' ');
            if (paramArray.Length != 4) return Binding.DoNothing;
            var thickness = new Thickness(System.Convert.ToDouble(paramArray[0])
                , System.Convert.ToDouble(paramArray[1])
                , System.Convert.ToDouble(width * multiple)
                , System.Convert.ToDouble(paramArray[3]));
            return thickness;

        }


        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
