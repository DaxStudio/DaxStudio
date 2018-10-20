namespace DaxStudio.UI.Events
{
    public class ChangeThemeEvent
    {
        public ChangeThemeEvent(string theme)
        {
            Theme = theme;
        }
        public string Theme { get; private set; }
    }
}
