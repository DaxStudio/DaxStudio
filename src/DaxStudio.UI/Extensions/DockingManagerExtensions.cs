using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvalonDock;
using System.IO;
using DaxStudio.Common;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.Extensions
{
    public static class DockingManagerExtensions
    {
        public static void LoadLayout(this DockingManager dockingManager)
        {
            var layoutFileName = ApplicationPaths.AvalonDockLayoutFile;
            if (!File.Exists(layoutFileName)) return; // exit here if the saved layout file does not exist

            using (StreamReader sr = new StreamReader(layoutFileName))
            {
            
                var ls = new AvalonDock.Layout.Serialization.XmlLayoutSerializer(dockingManager);
                //var ls = new DockingManagerJsonLayoutSerializer(dm);
                ls.Deserialize(sr);
            }
        }

        public static void SaveLayout(this DockingManager dockingManager)
        {
            var layoutFile = ApplicationPaths.AvalonDockLayoutFile;
            var layoutFolder = Path.GetDirectoryName(layoutFile);
            Directory.CreateDirectory(layoutFolder); //ensure that all the folders in the file path exist
            using (StreamWriter sw = new StreamWriter(layoutFile))
            {

                var ls = new AvalonDock.Layout.Serialization.XmlLayoutSerializer(dockingManager);
                ls.Serialize(sw);

            }
        }

        public static void RestoreDefaultLayout(this DockingManager dockingManager)
        {
            var thisAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            
            using (Stream strm = thisAssembly.GetManifestResourceStream(Constants.AvalonDockDefaultLayoutFile))
            {
                // check if default layout was found
                if (strm == null) return;

                var ls = new AvalonDock.Layout.Serialization.XmlLayoutSerializer(dockingManager);
                ls.Deserialize(strm);
            }

            // delete the saved layout file if it exists
            var layoutFileName = ApplicationPaths.AvalonDockLayoutFile;
            if (File.Exists(layoutFileName)) File.Delete(layoutFileName);
        }

    }
}
