namespace DaxStudio.Common.Interfaces
{
    public interface IThemeManager
    {
        string CurrentTheme { get; }

        void SetTheme(string themeName);
    }
}