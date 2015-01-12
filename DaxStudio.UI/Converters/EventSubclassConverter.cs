using System;
using System.Globalization;
using System.Windows.Data;
using Microsoft.AnalysisServices;

namespace DaxStudio.UI.Converters {
    class EventSubclassConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var sc = value as TraceEventSubclass?;
            if (sc != null) {
                switch ((TraceEventSubclass)sc) {
                    case TraceEventSubclass.VertiPaqCacheExactMatch:
                        return "Cache";
                    case TraceEventSubclass.VertiPaqScanInternal:
                        return "Internal";
                    case TraceEventSubclass.VertiPaqScan:
                        return "Scan";
                    default:
                        return sc.ToString();
                }
            }
            return System.Windows.Data.Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
