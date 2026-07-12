using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Core.Events
{
    public class TablesRefreshedEvent
    {
        public TablesRefreshedEvent() { }

        public TablesRefreshedEvent(object source)
        {
            Source = source;
        }

        /// <summary>
        /// The ConnectionManager instance that raised this event. Handlers should
        /// ignore events whose Source is not their own connection so that
        /// refreshing the tables on one document does not drive the metadata pane
        /// (and its busy overlay) of every other open document.
        /// May be null for legacy callers.
        /// </summary>
        public object Source { get; }
    }
}
