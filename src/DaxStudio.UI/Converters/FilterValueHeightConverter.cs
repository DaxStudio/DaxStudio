using DaxStudio.UI.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class FilterValueHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var ft = (FilterType)value;
            var paramArray =  parameter.ToString().Split(',');
            double bigHeight = Double.NaN;
            double smallHeight = Double.NaN;
            if (paramArray.Length == 1) bigHeight = System.Convert.ToDouble(paramArray[0]);
            if (paramArray.Length == 2)
            {
                bigHeight = System.Convert.ToDouble(paramArray[0]);
                smallHeight = System.Convert.ToDouble(paramArray[1]);
            }

            if (ft == FilterType.In || ft == FilterType.NotIn)
                return bigHeight;

            return smallHeight; // Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
