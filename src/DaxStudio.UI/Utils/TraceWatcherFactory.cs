using DaxStudio.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    internal class TraceWatcherFactory
    {
        public TraceWatcherFactory()
        {
            TraceWatchers = new List<ExportFactory<ITraceWatcher>>();
            var types = GetImplementingTypes(typeof(ITraceWatcher));
            foreach (var t in types)
            {
                //TraceWatchers.Add(new ExportFactory<ITraceWatcher>(() => t.New()));
            }
        }

        public List<ExportFactory<ITraceWatcher>> TraceWatchers { get; }

        public static IEnumerable<Type> GetImplementingTypes( Type itype)
            => AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes())
           .Where(t => t.GetInterfaces().Contains(itype));
    }
}
