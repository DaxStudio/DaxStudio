using DaxStudio.UI.Model;

namespace DaxStudio.UI.Events
{
    public class SetRunStyleEvent
    {
        public SetRunStyleEvent(RunStyleIcons icon)
        {
            Icon = icon;
        }
        public RunStyleIcons Icon { get; private set; }
    }
}
