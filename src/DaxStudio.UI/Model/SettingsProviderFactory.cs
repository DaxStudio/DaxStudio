using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Utils;
using System.IO;
using System.Reflection;

namespace DaxStudio.UI.Model
{
    public class SettingsProviderFactory
    {
        //[Export(typeof(Func<ISettingProvider>))]
        public ISettingProvider GetSettingProvider()
        {
            if (IsRunningPortable())
                return new JsonSettingProviderPortable();
            else
            //return new JsonSettingProviderAppData();

            // TODO if .portable file exists get JsonSettingsProviderPortable
            //      else get jsonSettingsProviderAppData

            // if registry keys exists load settings from Registry, save to Json and remove from registry
            return new RegistrySettingProvider();
        }

        private bool IsRunningPortable()
        {
            var exeFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            var portableFile = Path.Combine(exeFolder, ".portable");
            return File.Exists(portableFile);
        }
    }
}
