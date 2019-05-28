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
        }
        public RunQueryEvent(IResultsTarget target, RunStyle runStyle)
        {
            ResultsTarget = target;
            RunStyle = runStyle;
        }
        public IResultsTarget ResultsTarget { get; set; }

        public RunStyle RunStyle { get; }
    }
}
