using System.ComponentModel;
using Spectre.Console.Cli;

namespace DaxStudio.Common.Cli
{
    /// <summary>
    /// Spectre.Console.Cli settings class describing the command line options
    /// supported by the DAX Studio WPF launcher.
    /// </summary>
    public class LaunchSettings : CommandSettings
    {
        [CommandOption("-p|--port <PORT>")]
        [Description("Port number")]
        public int? Port { get; set; }

        [CommandOption("-l|--log [ENABLED]")]
        [Description("Enable Debug Logging")]
        [DefaultValue(true)]
        public FlagValue<bool> Log { get; set; }

        [CommandOption("-f|--file <FILE>")]
        [Description("Name of file to open")]
        public string FileName { get; set; }

        [CommandOption("-s|--server <SERVER>")]
        [Description("Server to connect to")]
        public string Server { get; set; }

        [CommandOption("-d|--database <DATABASE>")]
        [Description("Database to connect to")]
        public string Database { get; set; }

        [CommandOption("-r|--reset [ENABLED]")]
        [Description("Reset user preferences to the default settings")]
        [DefaultValue(true)]
        public FlagValue<bool> Reset { get; set; }

        [CommandOption("--nopreview [ENABLED]")]
        [Description("Hides version information")]
        [DefaultValue(true)]
        public FlagValue<bool> NoPreview { get; set; }

        [CommandOption("-u|--uri <URI>")]
        [Description("Used by the daxstudio:// uri handler")]
        public string Uri { get; set; }

#if DEBUG
        [CommandOption("-c|--crashtest [ENABLED]")]
        [Description("Triggers a crash for testing")]
        [DefaultValue(true)]
        public FlagValue<bool> CrashTest { get; set; }
#endif
    }
}
