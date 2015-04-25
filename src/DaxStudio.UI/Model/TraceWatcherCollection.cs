using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Model
{
    class TraceWatcherCollection:BindableCollection<ITraceWatcher>
    {
        public void ResetAll()
        {
            foreach (ITraceWatcher tw in this.Items)
            {
                tw.Reset();
            }
        }


    }
}
