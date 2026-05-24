using Caliburn.Micro;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.Tests.Mocks
{
    class MockEventAggregator : IEventAggregator
    {
        public bool HandlerExistsFor(Type messageType)
        {
            throw new NotImplementedException();
        }

        public void Subscribe(object subscriber, Func<Func<Task>, Task> marshal)
        {
            // do nothing
        }

        public void Unsubscribe(object subscriber)
        {
            // do nothing
        }

        public Task PublishAsync(object message, Func<Func<Task>, Task> marshal, CancellationToken cancellationToken = default)
        {
            // do nothing
            return Task.CompletedTask;
        }
    }
}
