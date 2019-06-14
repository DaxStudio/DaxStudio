using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.AvalonDock;

namespace DaxStudio.UI.Theme
{

    public class DaxStudioLightTheme : Xceed.Wpf.AvalonDock.Themes.Theme
    {
        public override Uri GetResourceUri()
        {
            var theme = new Xceed.Wpf.AvalonDock.Themes.MetroTheme();
            return theme.GetResourceUri();
            /*
            return new Uri(
                //"/DaxStudio.UI;component/Theme/Monotone.AvalonDock.xaml",
                "/DaxStudio.UI;component/Theme/Dark.AvalonDock.xaml",
                UriKind.Relative);
            */
        }
    }
    
}
