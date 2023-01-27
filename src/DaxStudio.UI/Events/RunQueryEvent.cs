using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.Events
{
    public class RunQueryEvent
    {
        public enum BenchmarkTypes
        {
            NoBenchmark = 0,
            QueryBenchmark,
            ServerFEBenchmark
        }

        public RunQueryEvent(IResultsTarget target)
        {
            ResultsTarget = target;
            RunStyle = new RunStyle("Run", RunStyleIcons.RunOnly,  "");
            BenchmarkType = BenchmarkTypes.NoBenchmark;
        }
        public RunQueryEvent(IResultsTarget target, RunStyle runStyle)
        {
            ResultsTarget = target;
            RunStyle = runStyle;
            BenchmarkType = BenchmarkTypes.NoBenchmark;
        }

        public RunQueryEvent(IResultsTarget target, RunStyle runStyle, BenchmarkTypes benchmarkType )
        {
            ResultsTarget = target;
            RunStyle = runStyle;
            BenchmarkType = benchmarkType;
        }
        public IResultsTarget ResultsTarget { get; set; }

        public RunStyle RunStyle { get; }

        public IQueryTextProvider QueryProvider { get; set; }
        public BenchmarkTypes BenchmarkType { get; set; }
    }
}
