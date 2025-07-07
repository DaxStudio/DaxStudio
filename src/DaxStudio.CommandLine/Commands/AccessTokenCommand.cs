using DaxStudio.CommandLine.Helpers;
using Spectre.Console;
using Spectre.Console.Cli;
using System;

namespace DaxStudio.CommandLine.Commands
{
    internal class AccessTokenCommand : Command<AccessTokenCommand.Settings>
    {
        internal class Settings : CommandSettingsRawBase
        {
            // No specific settings for this command
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            // No validation needed for this command
            return ValidationResult.Success();
        }
        public override int Execute(CommandContext context, Settings settings)
        {
            
                var accessToken = AccessTokenHelper.GetAccessToken(settings.FullConnectionString);
                Console.Write(accessToken.Token);
                return 0;

        }

    }

}
