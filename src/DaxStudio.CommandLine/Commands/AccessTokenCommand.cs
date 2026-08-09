using DaxStudio.CommandLine.Helpers;
using DaxStudio.Common;
using DaxStudio.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Threading;

namespace DaxStudio.CommandLine.Commands
{
    internal class AccessTokenCommand : Command<AccessTokenCommand.Settings>
    {
        private readonly IGlobalOptions _options;

        internal class Settings : CommandSettingsRawBase
        {
            // No specific settings for this command
        }

        public AccessTokenCommand(IGlobalOptions options = null)
        {
            _options = options;
        }

        protected override ValidationResult Validate(CommandContext context, Settings settings)
        {
            // No validation needed for this command
            return ValidationResult.Success();
        }
        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {            
            var accessToken = AccessTokenHelper.GetAccessToken(
                settings.FullConnectionString,
                settings.NonInteractive
                    ? EntraTokenAcquisitionMode.SilentOnly
                    : EntraTokenAcquisitionMode.SilentThenInteractive,
                _options);
            Console.Write(accessToken.Token);
            return 0;
        }

    }

}
