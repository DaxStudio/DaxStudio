using Spectre.Console.Cli;

namespace DaxStudio.CommandLine.Commands
{
    internal class CommandSettingsFileBase: CommandSettingsBase
    {
        [CommandArgument(0, "<OutputFile>")]
        public string OutputFile { get; set; }
    }
}
