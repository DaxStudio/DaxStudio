using DaxStudio.Common;
using System;

namespace DaxStudio.Core.Settings
{
    public class JsonSettingProviderAppData : JsonSettingProviderBase
    {
        public override string SettingsPath => ApplicationPaths.BasePath;
    }
}
