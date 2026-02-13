using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Events
{
    /// <summary>
    /// Event to show a generic tool window in the docking panel.
    /// </summary>
    public class ShowToolWindowEvent
    {
        public ShowToolWindowEvent(IToolWindow toolWindow)
        {
            ToolWindow = toolWindow;
        }

        public IToolWindow ToolWindow { get; private set; }
    }
}
