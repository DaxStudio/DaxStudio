using DaxStudio.Interfaces;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Serilog;

namespace DaxStudio.UI.Theme
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IThemeManager))]
    public class ThemeManager : IThemeManager, IDisposable
    {
        private Color _lightAccent = Color.FromRgb(34, 142, 214);
        private Color _darkAccent = Color.FromRgb(41, 169, 255);
        private bool disposedValue;

        [ImportingConstructor]
        public ThemeManager(IGlobalOptions options)
        {
            Options = options;
            CurrentTheme = Options.Theme;
            _app = Application.Current;
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        }


        public void SetTheme(string themeName)
        {
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ThemeManager), nameof(SetTheme), $"Setting Theme to: {themeName}");

            CurrentTheme = themeName;
            var windowsTheme = ThemeIsLight() ? "Light" : "Dark";
            var actualTheme = themeName=="Auto"?windowsTheme: themeName;
            //ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, $"{themeName}.DaxStudio");

            var theme = ModernWpf.ApplicationTheme.Light;
            Enum.TryParse(actualTheme, false, out theme);
            ModernWpf.ThemeManager.Current.ApplicationTheme = theme;
            SetAccent(AccentColor);

            //ControlzEx.Theming.ThemeManager.Current.ChangeThemeBaseColor(Application.Current, themeName);
            

        }

        private void SetAccent(Color accentColor)
        {

            ModernWpf.ThemeManager.Current.AccentColor = accentColor;

            Application.Current.Resources[AvalonDock.Themes.DaxStudio.Themes.ResourceKeys.ControlAccentColorKey] = accentColor;
            Application.Current.Resources[AvalonDock.Themes.DaxStudio.Themes.ResourceKeys.ControlAccentBrushKey] = new SolidColorBrush(accentColor);

            Application.Current.Resources[NumericUpDownLib.Themes.ResourceKeys.ControlAccentColorKey] = accentColor;
            Application.Current.Resources[NumericUpDownLib.Themes.ResourceKeys.ControlAccentBrushKey] = new SolidColorBrush(accentColor);

            //Application.Current.Resources[AvalonDock.Themes.VS2013.Themes.ResourceKeys.DocumentWellTabSelectedInactiveBackground] = accentColor;
            //Application.Current.Resources[AvalonDock.Themes.Themes.ResourceKeys.DocumentWellTabSelectedInactiveBackground] = accentColor;
        }

        public Color AccentColor { 
            get {
                if (CurrentTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase)) return _darkAccent;
                return _lightAccent;
            } 
        
        }
        public IGlobalOptions Options { get; }
        public string CurrentTheme { get; private set; }

        private readonly Application _app;


        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            switch (e.Category)
            {
                case UserPreferenceCategory.General:
                    // TODO: if options.theme is auto then set theme
                    SetTheme("Auto");
                    break;
            }
        }

        private static bool ThemeIsLight()
        {
            RegistryKey registry =
                Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return (int)registry.GetValue( "AppsUseLightTheme",1) == 1;
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
                }

                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ThemeManager()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
