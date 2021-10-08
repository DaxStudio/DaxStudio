using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public void Publish(object message, Action<System.Action> marshal)
        {
            // do nothing
        }

        public void Subscribe(object subscriber)
        {
            // do nothing
        }

        public void Unsubscribe(object subscriber)
        {
            // do nothing
        }

        public void PublishOnUIThread(object message)
        {
            // do nothing
        }

        public void Subscribe(object subscriber, Func<Func<Task>, Task> marshal)
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
