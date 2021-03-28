using DaxStudio.UI.Model;

namespace DaxStudio.UI.Events
{
    public class RunStyleChangedEvent
    {
        public RunStyleChangedEvent(RunStyle runStyle)
        {
            RunStyle = runStyle;
        }

        public RunStyle RunStyle { get; }
    }
}
