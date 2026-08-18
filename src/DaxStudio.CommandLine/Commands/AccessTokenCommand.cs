using DaxStudio.CommandLine.Helpers;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Threading;

namespace DaxStudio.CommandLine.Commands
{
    internal class AccessTokenCommand : Command<AccessTokenCommand.Settings>
    {
        private const string DefaultPowerBiConnectionString = "Data Source=powerbi://api.powerbi.com";

        internal class Settings : CommandSettingsRawBase
        {
            // No specific settings for this command
        }

        protected override ValidationResult Validate(CommandContext context, Settings settings)
        {
            return ValidationResult.Success();
        }

        internal static string GetTokenConnectionString(Settings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.Server) && string.IsNullOrWhiteSpace(settings.ConnectionString))
                return DefaultPowerBiConnectionString;

            return settings.FullConnectionString;
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {            
            var accessToken = AccessTokenHelper.GetAccessToken(GetTokenConnectionString(settings), settings);
            Console.Write(accessToken.Token);
            return 0;
        }

    }

}
