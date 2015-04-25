using System;
using System.Globalization;
using System.Windows.Data;
using Microsoft.AnalysisServices;
using DaxStudio.QueryTrace;

namespace DaxStudio.UI.Converters {
    class EventSubclassConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var sc = value as DaxStudioTraceEventSubclass?;
            if (sc != null) {
                switch ((DaxStudioTraceEventSubclass)sc) {
                    case DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch:
                        return "Cache";
                    case DaxStudioTraceEventSubclass.VertiPaqScanInternal:
                        return "Internal";
                    case DaxStudioTraceEventSubclass.VertiPaqScan:
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
