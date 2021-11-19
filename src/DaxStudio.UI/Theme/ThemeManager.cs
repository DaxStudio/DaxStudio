using DaxStudio.Interfaces;
using MLib.Interfaces;
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
        [ImportingConstructor]
        public ThemeManager(IAppearanceManager appearanceMgr, IGlobalOptions options)
        {
            AppearanceManager = appearanceMgr;
            Themes = appearanceMgr.CreateThemeInfos();
            AccentColor = Color.FromRgb(0, 114, 198);
            Options = options;
            CurrentTheme = Options.Theme;
            _app = Application.Current;
            LoadThemeResources();
        }

        private void LoadThemeResources()
        {
            Themes.RemoveAllThemeInfos();
            Themes.AddThemeInfo("Light", new List<Uri>() { });
            Themes.AddThemeInfo("Dark", new List<Uri>() { });

            AppearanceManager.AddThemeResources("Light", new List<Uri>
                {
                
                  //new Uri("/AvalonDock.Themes.VS2013;component/LightBrushs.xaml", UriKind.RelativeOrAbsolute)


                  //,new Uri("/DaxStudio.UI;component/Theme/AvalonDock_Dark_LightBrushs.xaml", UriKind.RelativeOrAbsolute)
                  //,new Uri("/DaxStudio.UI;component/Theme/Light.DaxStudio.xaml", UriKind.RelativeOrAbsolute)
                }, Themes);


            AppearanceManager.AddThemeResources("Dark", new List<Uri>
                {

                  //new Uri("/AvalonDock.Themes.VS2013;component/DarkBrushs.xaml", UriKind.RelativeOrAbsolute)
                 
                  //,new Uri("/DaxStudio.UI;component/Theme/AvalonDock_Dark_LightBrushs.xaml", UriKind.RelativeOrAbsolute)
                  
                  //,new Uri("/DaxStudio.UI;component/Theme/Dark.DaxStudio.xaml", UriKind.RelativeOrAbsolute)
                  //,new Uri("/DaxStudio.UI;component/Theme/Monotone.Colors.xaml", UriKind.RelativeOrAbsolute)
                  //,new Uri("/DaxStudio.UI;component/Theme/Monotone.Brushes.xaml", UriKind.RelativeOrAbsolute)
                  //,new Uri("/DaxStudio.UI;component/Theme/Monotone.xaml", UriKind.RelativeOrAbsolute)
                  //,new Uri("/DaxStudio.UI;component/Theme/Monotone.DaxEditor.xaml", UriKind.RelativeOrAbsolute)

                }, Themes);
        }

        public IAppearanceManager AppearanceManager { get; }

        public void SetTheme(string themeName)
        {
            CurrentTheme = themeName;
            AppearanceManager.SetTheme(Themes, themeName, AccentColor);

            ControlzEx.Theming.ThemeManager.Current.ChangeTheme(_app, $"{themeName}.DaxStudio");
        }

        public IThemeInfos Themes { get; }
        public Color AccentColor { get; }
        public IGlobalOptions Options { get; }
        public string CurrentTheme { get; private set; }

        private readonly Application _app;
    }
}
