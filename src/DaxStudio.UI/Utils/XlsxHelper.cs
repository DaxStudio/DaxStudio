using LargeXlsx;
using static LargeXlsx.XlsxAlignment;

namespace DaxStudio.UI.Utils
{
    internal static class XlsxHelper
    {
        public static XlsxStyle GetStyle(string systemType, string formatString)
        {
            // check for special case formatting
            switch (systemType.ToLower())
            {
                case "long":
                case "double":
                case "decimal":
                    if (!string.IsNullOrEmpty(formatString))
                        return XlsxStyle.Default.With(new XlsxNumberFormat(formatString));
                    break;
                case "datetime":
                    switch (formatString)
                    {
                        case "G": return XlsxStyle.Default.With(XlsxNumberFormat.ShortDateTime);
                        case "D": return XlsxStyle.Default.With(new XlsxNumberFormat("dddd, mmm dd, yyyy"));
                        default:
                            if (!string.IsNullOrEmpty(formatString))
                                return XlsxStyle.Default.With(new XlsxNumberFormat(formatString.ToLower().Replace("%", "")));
                            else
                                // default to short datetime
                                return XlsxStyle.Default.With(XlsxNumberFormat.ShortDateTime);
                    }



                case "string":
                    var stringAlignment = new XlsxAlignment(vertical: Vertical.Top, wrapText: true);
                    var stringStyle = XlsxStyle.Default.With(stringAlignment).With(XlsxNumberFormat.Text);
                    return stringStyle;

            }
            // if nothing else matches return the default style
            return XlsxStyle.Default;
        }
    }
}
