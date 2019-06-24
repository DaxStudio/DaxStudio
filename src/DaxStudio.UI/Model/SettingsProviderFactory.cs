using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using System;
using System.ComponentModel.Composition;

namespace DaxStudio.UI.Model
{
    public class SettingsProviderFactory
    {
        //[Export(typeof(Func<ISettingProvider>))]
        public ISettingProvider GetSettingProvider()
        {
            // TODO if .portable file exists get JsonSettingsProviderPortable
            //      else get jsonSettingsProviderAppData

            // if registry keys exists load settings from Registry, save to Json and remove from registry

            return new RegistrySettingProvider();
        }
    }
}
