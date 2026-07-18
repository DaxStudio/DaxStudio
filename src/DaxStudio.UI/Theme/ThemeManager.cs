using DaxStudio.Interfaces;
using Microsoft.Win32;
using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Media;
using Serilog;
using DaxStudio.Interfaces.Enums;
using DaxStudio.UI.Utils;

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

            // Ensure the non-client window frame (title bar / resize border) of every window
            // follows the active theme as each window is loaded and its handle becomes available.
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded));
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window) ApplyWindowChrome(window);
        }


        public void SetTheme(UITheme themeName)
        {
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ThemeManager), nameof(SetTheme), $"Setting Theme to: {themeName}");

            CurrentTheme = themeName;
            var windowsTheme = ThemeIsLight() ? "Light" : "Dark";
            Enum.TryParse<UITheme>(windowsTheme, out var enumWinTheme);
            Options.AutoTheme = enumWinTheme;
            EffectiveTheme = themeName==UITheme.Auto?enumWinTheme: themeName;
            
            // Set ModernWpf theme
            var theme = ModernWpf.ApplicationTheme.Light;
            Enum.TryParse(EffectiveTheme.ToString(), false, out theme);

            // exit here if the new theme is the same as the current theme 
            if (ModernWpf.ThemeManager.Current.ApplicationTheme == theme) return;

            ModernWpf.ThemeManager.Current.ApplicationTheme = theme;
            SetAccent(AccentColor);

            // Update the window frame (title bar / resize border) of any already open windows so
            // they no longer show a light system border while a dark theme is active.
            ApplyWindowChromeToAllWindows();
            
        }

        private bool IsDarkTheme => EffectiveTheme == UITheme.Dark;

        private static readonly Color DefaultDarkBorderColor = Color.FromRgb(0x2D, 0x2D, 0x2D);

        private Color WindowBorderColor
        {
            get
            {
                if (Application.Current?.Resources["Theme.Brush.Dialog.Border"] is SolidColorBrush brush)
                    return brush.Color;
                return DefaultDarkBorderColor;
            }
        }

        private void ApplyWindowChrome(Window window)
        {
            DwmHelper.ApplyThemeToWindow(window, IsDarkTheme, WindowBorderColor);
        }

        private void ApplyWindowChromeToAllWindows()
        {
            if (Application.Current == null) return;
            foreach (Window window in Application.Current.Windows)
            {
                ApplyWindowChrome(window);
            }
        }

        private void SetAccent(Color accentColor)
        {

            ModernWpf.ThemeManager.Current.AccentColor = accentColor;

            Application.Current.Resources[AvalonDock.Themes.DaxStudio.Themes.ResourceKeys.ControlAccentColorKey] = accentColor;
            Application.Current.Resources[AvalonDock.Themes.DaxStudio.Themes.ResourceKeys.ControlAccentBrushKey] = new SolidColorBrush(accentColor);

            Application.Current.Resources[NumericUpDownLib.Themes.ResourceKeys.ControlAccentColorKey] = accentColor;
            Application.Current.Resources[NumericUpDownLib.Themes.ResourceKeys.ControlAccentBrushKey] = new SolidColorBrush(accentColor);

        }

        public Color AccentColor { 
            get {
                if (EffectiveTheme == UITheme.Light) return _lightAccent;
                return _darkAccent;
            } 
        
        }
        public IGlobalOptions Options { get; }
        public UITheme CurrentTheme { get; private set; }
        private UITheme EffectiveTheme { get; set; }

        private readonly Application _app;

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            switch (e.Category)
            {
                case UserPreferenceCategory.General:
                    // update the theme to match the windows theme
                    if (Options.Theme == UITheme.Auto) SetTheme(UITheme.Auto);
                    break;
            }
        }

        private static bool ThemeIsLight()
        {
            RegistryKey registry =
                Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return (int)(registry?.GetValue( "AppsUseLightTheme",1)??1) == 1;
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
