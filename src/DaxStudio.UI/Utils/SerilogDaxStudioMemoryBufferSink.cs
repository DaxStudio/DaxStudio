using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using System;
using System.Text;
using System.IO;
using Serilog;
using Serilog.Configuration;
using Serilog.Formatting.Display;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// This class allows for Serilog messages to be sent to the output window in the current DaxStudio
    /// DocumentViewModel
    /// </summary>
    class SerilogMemoryBufferSink : ILogEventSink
    {

        readonly ITextFormatter _textFormatter;
        private static readonly FixedSizeQueue<string> _buffer = new FixedSizeQueue<string>(100);

        public SerilogMemoryBufferSink(ITextFormatter textFormatter, int maxEvents)
        {
            if (textFormatter == null) throw new ArgumentNullException("textFormatter");
            _textFormatter = textFormatter;
            _buffer.Limit = maxEvents;
            
        }

        
        public void Emit(LogEvent logEvent)
        {
                
            if (logEvent == null) throw new ArgumentNullException("logEvent");
            var sr = new StringWriter();
            _textFormatter.Format(logEvent, sr);

            var text = sr.ToString().Trim();

            _buffer.Enqueue(text);
            
        }

        public static string GetAllEvents()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var record in _buffer)
            {
                sb.AppendFormat("{0}\n", record);
            }
            return sb.ToString();
        }

    }

    public static class SerilogMemorySinkExtensions {
        const string DefaultConsoleOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}";
        //const string DefaultConsoleOutputTemplate = "[{Level}] {Message}{NewLine}{Exception}";
        /// <summary>
        /// This is how the serilog configuration knows about this sink
        /// </summary>
        /// <param name="sinkConfiguration"></param>
        /// <param name="restrictedToMinimumLevel"></param>
        /// <param name="outputTemplate"></param>
        /// <param name="formatProvider"></param>
        /// <returns></returns>
        public static LoggerConfiguration MemoryBuffer(
            this LoggerSinkConfiguration sinkConfiguration,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string outputTemplate = DefaultConsoleOutputTemplate,
            IFormatProvider formatProvider = null
            , int eventLimit = 50)
        {
            if (sinkConfiguration == null) throw new ArgumentNullException("sinkConfiguration");
            if (outputTemplate == null) throw new ArgumentNullException("outputTemplate");
            var formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
            return sinkConfiguration.Sink(new SerilogMemoryBufferSink(formatter, eventLimit), restrictedToMinimumLevel);
        }


    }
}

    
