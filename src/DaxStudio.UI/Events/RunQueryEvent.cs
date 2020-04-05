using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.Events
{
    public class RunQueryEvent
    {
        public RunQueryEvent(IResultsTarget target)
        {
            ResultsTarget = target;
            RunStyle = new RunStyle("Run", RunStyleIcons.RunOnly, false,false,false, "");
            IsBenchmark = false;
        }
        public RunQueryEvent(IResultsTarget target, RunStyle runStyle)
        {
            ResultsTarget = target;
            RunStyle = runStyle;
            IsBenchmark = false;
        }

        public RunQueryEvent(IResultsTarget target, RunStyle runStyle, bool isBenchmark)
        {
            ResultsTarget = target;
            RunStyle = runStyle;
            IsBenchmark = isBenchmark;
        }
        public IResultsTarget ResultsTarget { get; set; }

        public RunStyle RunStyle { get; }

        public IQueryTextProvider QueryProvider { get; set; }
        public bool IsBenchmark { get; set; }
    }
}
