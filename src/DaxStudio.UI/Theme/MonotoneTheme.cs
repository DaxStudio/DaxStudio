using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.AvalonDock;

namespace DaxStudio.UI.Theme
{

    public class MonotoneTheme : Xceed.Wpf.AvalonDock.Themes.Theme
    {
        public override Uri GetResourceUri()
        {
            return new Uri(
                //"/DaxStudio.UI;component/Theme/Monotone.AvalonDock.xaml",
                "/DaxStudio.UI;component/Theme/Dark.AvalonDock.xaml",
                UriKind.Relative);
        }
    }
    
}
