using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using System;
using Caliburn.Micro;
using System.IO;
using Serilog;
using Serilog.Configuration;
using Serilog.Formatting.Display;
using System.ComponentModel.Composition;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// This class allows for Serilog messages to be sent to the output window in the current DaxStudio
    /// DocumentViewModel
    /// </summary>
    class SerilogDaxStudioOutputSink : ILogEventSink
    {

        readonly ITextFormatter _textFormatter;

        public SerilogDaxStudioOutputSink(ITextFormatter textFormatter)
        {
            if (textFormatter == null) throw new ArgumentNullException("textFormatter");
            _textFormatter = textFormatter;
            
        }

        [Import]
        public IEventAggregator EventAggregator { get; private set; }

        public void Emit(LogEvent logEvent)
        {
            if (EventAggregator == null)
            {
                try
                {
                    this.EventAggregator = IoC.Get<IEventAggregator>();
                    EventAggregator.PublishOnUIThread(new DaxStudio.UI.Events.OutputMessage(Events.MessageType.Information, "Output Event Sink Started"));
                }
                catch { }
            }

            if (EventAggregator != null)
            {

                if (logEvent == null) throw new ArgumentNullException("logEvent");
                var sr = new StringWriter();
                _textFormatter.Format(logEvent, sr);

                var text = sr.ToString().Trim();

                if (logEvent.Level == LogEventLevel.Error || logEvent.Level == LogEventLevel.Fatal)
                    EventAggregator.PublishOnUIThread(new DaxStudio.UI.Events.OutputMessage(Events.MessageType.Error, text));
                else if (logEvent.Level == LogEventLevel.Warning)
                    EventAggregator.PublishOnUIThread(new DaxStudio.UI.Events.OutputMessage(Events.MessageType.Warning, text));
                else
                {
                    EventAggregator.PublishOnUIThread(new DaxStudio.UI.Events.OutputMessage(Events.MessageType.Information, text));
                }
            }
        }

        

    }

    public static class SerilogDaxStudioOutputSinkExtensions {
        //const string DefaultConsoleOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}";
        const string DefaultConsoleOutputTemplate = "[{Level}] {Message}{NewLine}{Exception}";
        /// <summary>
        /// This is how the serilog configuration knows about this sink
        /// </summary>
        /// <param name="sinkConfiguration"></param>
        /// <param name="restrictedToMinimumLevel"></param>
        /// <param name="outputTemplate"></param>
        /// <param name="formatProvider"></param>
        /// <returns></returns>
        public static LoggerConfiguration DaxStudioOutput(
            this LoggerSinkConfiguration sinkConfiguration,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            string outputTemplate = DefaultConsoleOutputTemplate,
            IFormatProvider formatProvider = null)
        {
            if (sinkConfiguration == null) throw new ArgumentNullException("sinkConfiguration");
            if (outputTemplate == null) throw new ArgumentNullException("outputTemplate");
            var formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
            return sinkConfiguration.Sink(new SerilogDaxStudioOutputSink(formatter), restrictedToMinimumLevel);
        }
    }
}

    
