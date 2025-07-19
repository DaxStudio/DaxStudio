using DaxStudio.CommandLine.Infrastructure;
using Spectre.Console;

namespace DaxStudio.CommandLine.Commands
{
    internal class CommandSettingsBase : CommandSettingsRawBase
    {
        public override ValidationResult Validate()
        {
            VersionInfo.Output();
            return base.Validate();
        }
    }
}
