using System;
using System.Globalization;
using System.Windows.Data;
using Microsoft.AnalysisServices;
using DaxStudio.QueryTrace;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.Converters {
    class EventClassSubclassConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var csc = value as DaxStudioTraceEventClassSubclass;
            var sc = value as DaxStudioTraceEventSubclass?;
            if (sc != null) {
                switch (csc.Subclass) {
                    case DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch:
                        return "Cache";
                    case DaxStudioTraceEventSubclass.VertiPaqScanInternal:
                        return "Internal";
                    case DaxStudioTraceEventSubclass.VertiPaqScan:
                        return "Scan";
                    case DaxStudioTraceEventSubclass.BatchVertiPaqScan:
                        return "Batch";
                    default:
                        return csc.ToString();
                }
            } else if (csc != null) {
                switch (csc.Subclass) {
                    case DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch:
                        return "Cache";
                    case DaxStudioTraceEventSubclass.VertiPaqScanInternal:
                        return "Internal";
                    case DaxStudioTraceEventSubclass.VertiPaqScan:
                        return "Scan";
                    case DaxStudioTraceEventSubclass.BatchVertiPaqScan:
                        return "Batch";
                    case DaxStudioTraceEventSubclass.NotAvailable:
                        switch (csc.Class) {
                            case DaxStudioTraceEventClass.DirectQueryEnd:
                                return csc.QueryLanguage.ToString();
                            default:
                                return csc.Class.ToString();
                        }
                    default:
                        return csc.Subclass.ToString();
                }
            }
            return System.Windows.Data.Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
