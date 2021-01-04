using System.Windows;

namespace DaxStudio.UI.Events
{
    public class EditorResizeEvent
    {
        public EditorResizeEvent(Size size)
        {
            NewSize = size;
        }

        public Size NewSize { get; }
    }
}
