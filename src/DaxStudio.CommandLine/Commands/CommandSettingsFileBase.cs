using Spectre.Console.Cli;

namespace DaxStudio.CommandLine.Commands
{
    internal class CommandSettingsFileBase: CommandSettingsRawBase
    {
        [CommandArgument(0, "<OutputFile>")]
        public string OutputFile { get; set; }
    }
}
