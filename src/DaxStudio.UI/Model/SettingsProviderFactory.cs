using DaxStudio.Common;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Utils;
using System.IO;
using System.Reflection;

namespace DaxStudio.UI.Model
{
    public static class SettingsProviderFactory
    {
        //[Export(typeof(Func<ISettingProvider>))]
        public static ISettingProvider GetSettingProvider()
        {
            if (IsRunningPortable)
                return new JsonSettingProviderPortable();
            else
            //return new JsonSettingProviderAppData();

            // TODO if .portable file exists get JsonSettingsProviderPortable
            //      else get jsonSettingsProviderAppData

            // if registry keys exists load settings from Registry, save to Json and remove from registry
            return new RegistrySettingProvider();
        }

        private static bool IsRunningPortable => ApplicationPaths.IsInPortableMode;
    }
}
