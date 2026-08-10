using DaxStudio.CommandLine.Helpers;
using DaxStudio.CommandLine.Interfaces;
using DaxStudio.Common;
using DaxStudio.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Data.OleDb;
using System.Threading;

namespace DaxStudio.CommandLine.Commands
{
    internal class AuthCommand : Command<AuthCommand.Settings>
    {
        private readonly IGlobalOptions _options;

        internal class Settings : CommandSettings, ISettingsConnection
        {
            [CommandOption("-s|--server <server>")]
            [Description("The Power BI or Azure Analysis Services endpoint to authenticate")]
            public string Server { get; set; }

            [CommandOption("-c|--connectionstring <connectionString>")]
            [Description("A connection string containing the endpoint to authenticate")]
            public string ConnectionString { get; set; }

            [CommandOption("--non-interactive")]
            [Description("Never open an authentication prompt; fail if cached sign-in cannot be used")]
            public bool NonInteractive { get; set; }

            public string FullConnectionString
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(ConnectionString))
                        return new OleDbConnectionStringBuilder(ConnectionString).ConnectionString;

                    var builder = new OleDbConnectionStringBuilder();
                    if (!string.IsNullOrWhiteSpace(Server))
                        builder["Data Source"] = Server;
                    return builder.ConnectionString;
                }
            }

            public override ValidationResult Validate()
            {
                if (!string.IsNullOrWhiteSpace(Server)
                    && !string.IsNullOrWhiteSpace(ConnectionString))
                    return ValidationResult.Error(
                        "You cannot specify both <server> and <connectionstring>");

                if (string.IsNullOrWhiteSpace(Server)
                    && string.IsNullOrWhiteSpace(ConnectionString))
                    return ValidationResult.Error(
                        "You must specify either <server> or <connectionstring>");

                try
                {
                    var builder = new OleDbConnectionStringBuilder(FullConnectionString);
                    if (string.IsNullOrWhiteSpace(builder.DataSource))
                        return ValidationResult.Error(
                            "The connection string must specify a data source");

                    if (UsesPasswordOrServicePrincipal(builder))
                        return ValidationResult.Error(
                            "The auth command prepares the delegated sign-in cache; password and service-principal connection strings are not supported");

                    if (!AccessTokenHelper.IsAccessTokenNeeded(builder.ConnectionString))
                        return ValidationResult.Error(
                            "The auth command requires a Power BI or Azure Analysis Services endpoint that uses delegated Entra authentication");
                }
                catch (ArgumentException ex)
                {
                    return ValidationResult.Error($"Invalid connection string: {ex.Message}");
                }

                return base.Validate();
            }

            private static bool UsesPasswordOrServicePrincipal(
                OleDbConnectionStringBuilder builder)
            {
                if (builder.ContainsKey("Password") || builder.ContainsKey("Pwd"))
                    return true;

                if (!builder.ContainsKey("User ID"))
                    return false;

                return Convert.ToString(builder["User ID"])
                    .StartsWith("app:", StringComparison.OrdinalIgnoreCase);
            }
        }

        public AuthCommand(IGlobalOptions options = null)
        {
            _options = options;
        }

        protected override int Execute(
            CommandContext context,
            Settings settings,
            CancellationToken cancellationToken)
        {
            var accessToken = AccessTokenHelper.GetAccessToken(
                settings.FullConnectionString,
                AccessTokenHelper.GetAcquisitionMode(settings),
                _options);
            var tokenContext = accessToken.UserContext as AccessTokenContext;

            AnsiConsole.MarkupLine(
                CreateSuccessMessage(tokenContext?.Username, accessToken.ExpirationTime));
            return 0;
        }

        internal static string CreateSuccessMessage(
            string username,
            DateTimeOffset expirationTime)
        {
            var expiration = expirationTime.ToUniversalTime().ToString("u");
            return string.IsNullOrWhiteSpace(username)
                ? $"[green]Authentication cache is ready.[/] Cached sign-in expires {expiration}."
                : $"[green]Authentication cache is ready.[/] Account: [bold]{Markup.Escape(username)}[/]; expires {expiration}.";
        }
    }
}
