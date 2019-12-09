using DaxStudio.Common;
using System;

namespace DaxStudio.UI.Utils
{
    public class JsonSettingProviderAppData : JsonSettingProviderBase
    {
        public override string SettingsPath => ApplicationPaths.BasePath;
    }
}
