using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvalonDock;

namespace DaxStudio.UI.Theme
{

    public class DaxStudioLightTheme : AvalonDock.Themes.Theme
    {
        

        public override Uri GetResourceUri()
        {
            return new Uri(
                "/DaxStudio.UI;component/Theme/Light.AvalonDock.xaml",
                UriKind.Relative);
        }
    }
    
}
