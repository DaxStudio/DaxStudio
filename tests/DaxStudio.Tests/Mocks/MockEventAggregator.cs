using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.Tests.Mocks
{
    class MockEventAggregator : IEventAggregator
    {
        public List<object> PublishedMessages { get; } = new List<object>();

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
            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }
    }
}
