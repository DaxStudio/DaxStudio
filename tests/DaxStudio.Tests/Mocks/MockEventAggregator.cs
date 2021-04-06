using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    }
}
