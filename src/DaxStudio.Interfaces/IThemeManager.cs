using System;

namespace DaxStudio.Interfaces
{
    public interface IThemeManager: IDisposable
    {
        string CurrentTheme { get; }

        void SetTheme(string themeName);
    }
}