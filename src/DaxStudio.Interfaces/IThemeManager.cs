using DaxStudio.Interfaces.Enums;
using System;

namespace DaxStudio.Interfaces
{
    public interface IThemeManager: IDisposable
    {
        UITheme CurrentTheme { get; }

        void SetTheme(UITheme themeName);
    }
}