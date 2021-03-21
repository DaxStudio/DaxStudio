using DaxStudio.Interfaces;

namespace DaxStudio.UI.Extensions
{
    public static class IGlobalOptionsExtensions
    {
        public static bool AnyExternalAccessAllowed(this IGlobalOptions options)
        {
            return !options.BlockVersionChecks || !options.BlockExternalServices || !options.BlockCrashReporting;
        }
    }
}
