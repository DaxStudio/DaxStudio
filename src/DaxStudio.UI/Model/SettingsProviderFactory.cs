using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Utils;

namespace DaxStudio.UI.Model
{
    public class SettingsProviderFactory
    {
        //[Export(typeof(Func<ISettingProvider>))]
        public ISettingProvider GetSettingProvider()
        {
            //return new JsonSettingProviderAppData();

            // TODO if .portable file exists get JsonSettingsProviderPortable
            //      else get jsonSettingsProviderAppData

            // if registry keys exists load settings from Registry, save to Json and remove from registry

            return new RegistrySettingProvider();
        }
    }
}
