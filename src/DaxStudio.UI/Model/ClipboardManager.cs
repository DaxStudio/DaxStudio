using DaxStudio.UI.Extensions;
using Polly;
using Polly.Retry;
using Serilog;
using System;
using System.Windows;
using static System.Net.Mime.MediaTypeNames;

namespace DaxStudio.UI.Model
{
    static internal class ClipboardManager
    {
        static RetryPolicy _retry;
        static ClipboardManager()
        {

                _retry = Policy
                    .HandleInner<System.Runtime.InteropServices.COMException>()
                    .WaitAndRetry(3, retryCount => TimeSpan.FromMilliseconds(200),
                        (exception, timespan, retryCount, context) =>
                        {
                            var text = context.GetText();
                            // attempt to set the clipboard text again
                            System.Windows.Clipboard.SetText(text);
                            Log.Warning(exception, Common.Constants.LogMessageTemplate, nameof(ClipboardManager),
                                "CopyRetryPolicy", exception.Message);
                        });
            
        }
        public static void SetText(string text)
        {
            var context = new Polly.Context().WithText(text).WithTextDataFormat(TextDataFormat.Text);
            _retry.Execute((ctx) => { 
                System.Windows.Clipboard.SetText(text);
            },context);
        }

        internal static void SetText(string text, TextDataFormat format)
        {
            var context = new Polly.Context().WithText(text).WithTextDataFormat(format);
            _retry.Execute((ctx) => {
                System.Windows.Clipboard.SetText(text,format);
            }, context);
        }
    }
}
