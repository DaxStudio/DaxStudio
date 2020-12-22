using ADOTabular;
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
    public class IsMeasureVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ADOTabularObjectType objectType)
            {
                switch ( objectType)
                {
                    case ADOTabularObjectType.Measure:
                    case ADOTabularObjectType.KPI:
                    case ADOTabularObjectType.KPIGoal:
                    case ADOTabularObjectType.KPIStatus:
                        return Visibility.Visible;
                    default:
                        return Visibility.Collapsed;
                }

            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
