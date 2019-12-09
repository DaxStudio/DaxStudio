using System;

namespace DaxStudio.UI.Utils
{
    public class JsonSettingProviderPortable : JsonSettingProviderBase
    {
        public override string SettingsPath => AppDomain.CurrentDomain.BaseDirectory;
        public override bool IsRunningPortable => true;
    }
}
