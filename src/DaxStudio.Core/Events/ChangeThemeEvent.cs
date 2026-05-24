using DaxStudio.Interfaces.Enums;

namespace DaxStudio.Core.Events
{
    public class ChangeThemeEvent
    {
        public ChangeThemeEvent(UITheme theme)
        {
            Theme = theme;
        }
        public UITheme Theme { get; private set; }
    }
}
