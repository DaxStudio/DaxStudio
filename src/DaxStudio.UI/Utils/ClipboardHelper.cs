using Polly;
using Serilog;
using System;
using System.Windows;

namespace DaxStudio.UI.Utils
{
    public static class ClipboardHelper
    {
        private static Policy _retryPolicy;
        static ClipboardHelper()
        {
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(
                3,
                retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100),
                (exception, timeSpan, context) => {
                    Log.Error(exception, "{class} {method}", nameof(ClipboardHelper), nameof(GetText));
                    System.Diagnostics.Debug.WriteLine("Error getting clipboard text during paste: " + exception.Message);
                }
            ); 
        }

        public static Tuple<string,LongLineStateMachine> GetText(IDataObject dataObject )
        {
            string content = null;
            _retryPolicy.Execute(() =>
            {
                if (dataObject.GetDataPresent(DataFormats.UnicodeText))
                    content = dataObject.GetData(DataFormats.UnicodeText, true) as string;
                else if (dataObject.GetDataPresent(DataFormats.Text))
                    content = dataObject.GetData(DataFormats.Text, true) as string;
                else if (dataObject.GetDataPresent(DataFormats.OemText))
                    content = dataObject.GetData(DataFormats.OemText, true) as string;
            });

            var sm = new LongLineStateMachine(Common.Constants.MaxLineLength);
            var newContent = sm.ProcessString(content);

            return Tuple.Create(newContent, sm );
        }

        public static void SetDataObject(string text, IDataObject dataObject )
        {
            _retryPolicy.Execute(() =>
            {
                dataObject = new DataObject(text);
            });
        }

    }
}
