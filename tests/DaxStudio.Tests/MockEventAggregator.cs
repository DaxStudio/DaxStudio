using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    class MockEventAggregator : IEventAggregator
    {
        public bool HandlerExistsFor(Type messageType)
        {
            throw new NotImplementedException();
        }

        public void Publish(object message, Action<System.Action> marshal)
        {
            throw new NotImplementedException();
        }

        public void Subscribe(object subscriber)
        {
            throw new NotImplementedException();
        }

        public void Unsubscribe(object subscriber)
        {
            throw new NotImplementedException();
        }
    }
}
