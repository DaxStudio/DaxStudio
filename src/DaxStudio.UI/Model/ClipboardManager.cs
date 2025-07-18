using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using DaxStudio.UI.Extensions;
using Polly;
using Polly.Retry;
using Serilog;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;

namespace DaxStudio.UI.Model
{
    static internal class ClipboardManager
    {
        static RetryPolicy _retrySetText;
        static RetryPolicy _retrySetData;
        const int MaxRetryCount = 3;
        const int retryDelay = 50;
        static ClipboardManager()
        {

            _retrySetText = Policy
                .Handle<System.Runtime.InteropServices.COMException>()
                .OrInner<COMException>()
                .WaitAndRetry(MaxRetryCount, retryCount => TimeSpan.FromMilliseconds(retryDelay * retryCount),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        Log.Warning(exception, Common.Constants.LogRetryMessageTemplate, nameof(ClipboardManager),
                            "ClipboardManagerSetTextRetryPolicy", timespan, retryCount, MaxRetryCount, exception.Message);       
                    });

            _retrySetData = Policy
                .Handle<System.Runtime.InteropServices.COMException>()
                .OrInner<COMException>()
                .WaitAndRetry(MaxRetryCount, retryCount => TimeSpan.FromMilliseconds(retryDelay * retryCount),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        Log.Warning(exception, Common.Constants.LogRetryMessageTemplate, 
                            nameof(ClipboardManager),
                            "ClipboardManagerSetDataRetryPolicy", timespan, retryCount, MaxRetryCount , exception.Message);
                    });

        }
        public static void SetText(string text)
        {
            SetText(text, TextDataFormat.Text);
        }

        private static void SetTextImpl(string text, TextDataFormat format = TextDataFormat.Text)
        {
            if (string.IsNullOrEmpty(text)) return;         
            if (format == TextDataFormat.Text && text.Contains("\r\n"))
            {
                // if the text contains line breaks then we need to use the UnicodeText format
                format = TextDataFormat.UnicodeText;
            }
            //Clipboard.SetText(text, format);
            DataObject dataObject = new DataObject(format == TextDataFormat.Text?  DataFormats.Text : DataFormats.UnicodeText,text,true);
            Clipboard.SetDataObject(dataObject, true);
            Log.Verbose(Constants.LogMessageTemplate, nameof(ClipboardManager), nameof(SetTextImpl), "Clipboard Text set");
        }

        internal static void SetText(string text, TextDataFormat format)
        {
            _retrySetText.Execute(() => {
                SetTextImpl(text,format);
            });
        }

        private static void SetDataImpl(string format, object data)
        {
            if (string.IsNullOrEmpty(format) || data == null) return;
            System.Windows.Clipboard.SetData(format, data);
        }

        static readonly string HEADER =
            "Version:0.9\r\n" +
            "StartHTML:{0:0000000000}\r\n" +
            "EndHTML:{1:0000000000}\r\n" +
            "StartFragment:{2:0000000000}\r\n" +
            "EndFragment:{3:0000000000}\r\n";

        static readonly string HTML_START =
            "<html>\r\n" +
            "<body>\r\n" +
            "<!--StartFragment-->";

        static readonly string HTML_END =
            "<!--EndFragment-->\r\n" +
            "</body>\r\n" +
            "</html>";

        internal static string InsertHtmlHeader(string links, string fragment)
        {
            const int headerLen = 105;
            var htmlStartLen = HTML_START.Length;
            var htmlEndLen = HTML_END.Length;
            StringBuilder sb = new StringBuilder();
            var header = string.Format(HEADER, headerLen, headerLen + htmlStartLen +links.Length + fragment.Length + htmlEndLen, headerLen + htmlStartLen, headerLen + htmlStartLen + links.Length + fragment.Length);
            sb.Append(header);
            sb.Append(HTML_START);
            sb.Append(links);
            sb.Append(fragment);
            sb.Append(HTML_END);
            return sb.ToString();
        }

        private const string Clipboard_RichText_Format = "Rich Text Format";
        private const string Clipboard_Html_Format = "HTML Format";
        private const string Clipboard_Text_Format = "Text";
        private static Regex regexHtmlFragment = new Regex("(?<=<!--StartFragment-->)(.*)(?=<!--EndFragment-->)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        internal static void AddHyperlinkHeaderToQuery(IDataObject data, ConnectionManager connection)
        {
            try
            {
                if (!data.GetDataPresent(Clipboard_Html_Format)) return;
                if (!data.GetDataPresent(Clipboard_Text_Format)) return;

                var html = (string)data.GetData(Clipboard_Html_Format);
                var text = (string)data.GetData(Clipboard_Text_Format);
                var fragment = regexHtmlFragment.Match((string)html).Value;
                var link = $"daxstudio:?server={HttpUtility.UrlEncode(connection.ServerName)}&database={HttpUtility.UrlEncode(connection.DatabaseName)}&query={text.Base64Encode()}";
                var linkHtml = $"<div><a href='{link}' Style='Font-Size:9px'>Open in DAX Studio</a></div>";
                var newHtml = InsertHtmlHeader(linkHtml, fragment);

                _retrySetData.Execute(() =>
                {
                    SetDataImpl(Clipboard_Html_Format, newHtml);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(ClipboardManager), nameof(AddHyperlinkHeaderToQuery));
            }
        }

        internal static void ReplaceLineBreaks(IDataObject data)
        {
            
            if (data == null) return;

            try
            {
                if (!data.GetDataPresent(Clipboard_RichText_Format)) return;

                var richText = (string)data.GetData(Clipboard_RichText_Format);
                // Replaces "hard" paragraph breaks with line breaks
                // in Word this does not replace the paragraph style
                // and only keeps the character style
                var newRichText = richText.Replace("\\par", "\\line");

                _retrySetData.Execute(() =>
                {
                    SetDataImpl(Clipboard_RichText_Format, newRichText);
                });
                
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(ClipboardManager), nameof(ReplaceLineBreaks));
            }
        }
    }
}
