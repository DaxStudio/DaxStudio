using System;

namespace DaxStudio.Core.Settings
{
    public class JsonSettingProviderPortable : JsonSettingProviderBase
    {
        public override string SettingsPath => AppDomain.CurrentDomain.BaseDirectory;
        public override bool IsRunningPortable => true;
    }
}
