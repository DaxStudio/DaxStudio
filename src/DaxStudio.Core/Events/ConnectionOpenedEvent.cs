using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Core.Events
{
    public class ConnectionOpenedEvent
    {
        public ConnectionOpenedEvent() { }

        public ConnectionOpenedEvent(object source)
        {
            Source = source;
        }

        /// <summary>
        /// The ConnectionManager instance that
        /// raised this event. Handlers should ignore events whose Source is not
        /// their own connection so that opening a connection on one document
        /// (e.g. the temporary document spawned by Capture Diagnostics) does
        /// not blank the metadata of every other open document.
        /// May be null for legacy callers.
        /// </summary>
        public object Source { get; }
    }
}
