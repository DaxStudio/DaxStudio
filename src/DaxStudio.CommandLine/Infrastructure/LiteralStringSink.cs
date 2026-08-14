using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using System.Linq;

namespace DaxStudio.CommandLine.Infrastructure
{
    /// <summary>
    /// Renders string values in log messages without surrounding quotes, then forwards the event to
    /// another sink.
    /// </summary>
    /// <remarks>
    /// Serilog quotes string values unless the property is formatted with <c>:l</c>, which the
    /// output template normally requests once for the whole message via <c>{Message:lj}</c>.
    /// Serilog.Sinks.Spectre renders the message itself so that it can colour each value, and in
    /// doing so it ignores that format specifier - every string would come out quoted
    /// (<c>Using "UserID" argument</c>, <c>Error: "No connection could be made..."</c>).
    ///
    /// Rather than annotate every call site with <c>:l</c>, which is easy to forget and would have
    /// to be repeated forever, this applies the same format to string values on the way through.
    /// </remarks>
    internal sealed class LiteralStringSink : ILogEventSink
    {
        private const string LiteralFormat = "l";

        private readonly ILogEventSink _inner;

        public LiteralStringSink(ILogEventSink inner)
        {
            _inner = inner;
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null) return;
            _inner.Emit(WithLiteralStrings(logEvent));
        }

        private static LogEvent WithLiteralStrings(LogEvent logEvent)
        {
            var tokens = logEvent.MessageTemplate.Tokens.ToList();
            var rewrittenAny = false;

            for (var i = 0; i < tokens.Count; i++)
            {
                if (!(tokens[i] is PropertyToken property)) continue;
                if (property.Format != null) continue;  // an explicit format wins
                if (!IsStringValue(logEvent, property)) continue;

                tokens[i] = new PropertyToken(
                    property.PropertyName,
                    property.ToString(),
                    LiteralFormat,
                    property.Alignment,
                    property.Destructuring);
                rewrittenAny = true;
            }

            if (!rewrittenAny) return logEvent;

            return new LogEvent(
                logEvent.Timestamp,
                logEvent.Level,
                logEvent.Exception,
                new MessageTemplate(tokens),
                logEvent.Properties.Select(p => new LogEventProperty(p.Key, p.Value)));
        }

        private static bool IsStringValue(LogEvent logEvent, PropertyToken property)
            => logEvent.Properties.TryGetValue(property.PropertyName, out var value)
               && value is ScalarValue scalar
               && scalar.Value is string;
    }
}
