using Caliburn.Micro;
using System;

namespace DaxStudio.Core.Events
{
    public class OutputMessage : PropertyChangedBase
    {
        private readonly double _durationMs = double.NaN;
        internal OutputMessage() { }

        public OutputMessage(MessageType messageType, string text, double durationMs) : this(messageType, text)
        {
            _durationMs = durationMs;
        }

        public OutputMessage(MessageType messageType, string text)
        {
            Text = text;
            MessageType = messageType;
            Start = DateTime.Now;
            _durationMs = double.NaN;
        }

        /// <summary>
        /// Optional reference to the host that displays this message.
        /// Typed as <see cref="object"/> so Core can publish output messages
        /// without depending on the UI <c>OutputPaneViewModel</c>.
        /// UI subclasses that need to navigate (e.g. open folders or jump
        /// to a source location) should cast this to their expected host type.
        /// </summary>
        public object Parent { get; set; }
        public bool ActivateOutput { get; set; }

        public string Text { get; set; }
        public DateTime Start { get; set; }
        public MessageType MessageType { get; set; }


        public double DurationMs { get { return _durationMs; } }
        public string DurationString
        {
            get
            {
                if (double.IsNaN(_durationMs))
                    return string.Empty;
                return _durationMs.ToString("#,##0");
            }
        }

        public string DurationTooltip
        {
            get
            {
                if (double.IsNaN(DurationMs)) return string.Empty;
                return $"{DurationString} ms  ({TimeSpan.FromMilliseconds(DurationMs):h\\:mm\\:ss\\.fff})";
            }
        }

    }
}
