using Caliburn.Micro;
using DaxStudio.UI.Events;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine
{
    internal class ConsoleLogger : IHandle<OutputMessage>
    {

        public Task HandleAsync(OutputMessage message, CancellationToken cancellationToken)
        {
            switch (message.MessageType)
            {
                case MessageType.Error:
                    Log.Error(message.Text);
                    break;
                case MessageType.Warning:
                    Log.Warning(message.Text);
                    break;
                default:
                    Log.Information(message.Text);
                    break;
            }
            return Task.CompletedTask;
        }
    }
}
