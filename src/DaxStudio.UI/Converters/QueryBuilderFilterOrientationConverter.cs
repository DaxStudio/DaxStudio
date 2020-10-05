using DaxStudio.UI.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class QueryBuilderFilterOrientationConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[1] == System.Windows.DependencyProperty.UnsetValue) return Orientation.Vertical;
            double actWidth = (double)values[0];
            FilterType ft = (FilterType)values[1];

            if (ft == FilterType.Between && actWidth > 400) return Orientation.Horizontal;
            if (ft != FilterType.Between && actWidth > 250) return Orientation.Horizontal;
            return Orientation.Vertical;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
            //return Binding.DoNothing;
        }
    }
}
