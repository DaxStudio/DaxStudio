using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace DaxStudio.UI.Theme
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IThemeManager))]
    public class ThemeManager : IThemeManager
    {
        private Color _lightAccent = Color.FromRgb(34, 142, 214);
        private Color _darkAccent = Color.FromRgb(41, 169, 255);

        [ImportingConstructor]
        public ThemeManager(IGlobalOptions options)
        {
            Options = options;
            CurrentTheme = Options.Theme;
            _app = Application.Current;
        }


        public void SetTheme(string themeName)
        {
            CurrentTheme = themeName;
            var theme = ModernWpf.ApplicationTheme.Light;
            Enum.TryParse(themeName, false, out theme);
            ModernWpf.ThemeManager.Current.ApplicationTheme = theme;
            SetAccent(AccentColor);

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
    }
}
