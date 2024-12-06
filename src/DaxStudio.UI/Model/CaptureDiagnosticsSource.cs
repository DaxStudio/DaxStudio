using DaxStudio.UI.Interfaces;
using System.Collections.Generic;

namespace DaxStudio.UI.Model
{
    public enum DiagnosticSources
    {
        ActiveDocument,
        AllQueries,
        Clipboard,
        PerformanceData
    }

    public class CaptureDiagnosticsSource
    {
        public CaptureDiagnosticsSource(DiagnosticSources source, IEnumerable<IQueryTextProvider> queries)
        {
            Source = source;
            Queries = queries;
        }

        public DiagnosticSources Source { get; }
        public IEnumerable<IQueryTextProvider> Queries { get;  }
    }
}
