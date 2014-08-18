using System;

namespace DaxStudio.UI.Events
{
    public class OutputMessage
    {
        private readonly double _durationMs = double.NaN;
        public OutputMessage(MessageType messageType, string text, double durationMs)
        {
            Text = text;
            MessageType = messageType;
            Start = DateTime.Now;
            _durationMs = durationMs;
        }

        public OutputMessage(MessageType messageType, string text)
        {
            Text = text;
            MessageType = messageType;
            Start = DateTime.Now;
            _durationMs = double.NaN;
        }

        public string Text { get; set; }
        public DateTime Start { get; set; }
        public MessageType MessageType { get; set; }
        public string Duration {
            get
            {
                if (double.IsNaN(_durationMs ))
                    return string.Empty;
                return _durationMs.ToString("#,##0");
            }
        }
    }

    public enum MessageType
    {
        Information
        ,Warning
        ,Error
    }
     
}
