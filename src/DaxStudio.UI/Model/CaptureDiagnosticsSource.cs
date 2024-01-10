using DaxStudio.UI.Interfaces;
using System.Collections.Generic;

namespace DaxStudio.UI.Model
{
    public class CaptureDiagnosticsSource
    {
        public CaptureDiagnosticsSource(string name, IEnumerable<IQueryTextProvider> queries)
        {
            Name = name;
            Queries = queries;
        }

        public string Name { get; }
        public IEnumerable<IQueryTextProvider> Queries { get;  }
    }
}
