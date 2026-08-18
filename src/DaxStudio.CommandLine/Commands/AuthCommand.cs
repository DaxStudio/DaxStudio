using DaxStudio.CommandLine.Helpers;
using DaxStudio.Common;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace DaxStudio.CommandLine.Commands
{
    internal class AuthCommand : Command<AuthCommand.Settings>
    {
        private const string DefaultPowerBiConnectionString = "Data Source=powerbi://api.powerbi.com";

        internal class Settings : CommandSettingsRawBase
        {
            [CommandOption("--list")]
            [Description("Lists accounts available from the DAX Studio cache and Windows without acquiring a token")]
            public bool List { get; set; }

            public override ValidationResult Validate()
            {
                if (!string.IsNullOrWhiteSpace(ConnectionString)
                    && (!string.IsNullOrWhiteSpace(Server) || !string.IsNullOrWhiteSpace(Database)))
                {
                    return ValidationResult.Error("You cannot specify a <Server> or <Database> when passing a <ConnectionString>");
                }

                if (string.IsNullOrWhiteSpace(ConnectionString)
                    && !string.IsNullOrWhiteSpace(Database)
                    && string.IsNullOrWhiteSpace(Server))
                {
                    return ValidationResult.Error("You must specify a <server> when using the <database> parameter");
                }

                return ValidationResult.Success();
            }
        }

        internal static string GetAuthenticationConnectionString(Settings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.Server) && string.IsNullOrWhiteSpace(settings.ConnectionString))
                return DefaultPowerBiConnectionString;

            return settings.FullConnectionString;
        }

        protected override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (settings.List && (!string.IsNullOrWhiteSpace(settings.Server)
                || !string.IsNullOrWhiteSpace(settings.Database)
                || !string.IsNullOrWhiteSpace(settings.ConnectionString)
                || !string.IsNullOrWhiteSpace(settings.UserID)
                || !string.IsNullOrWhiteSpace(settings.Password)
                || settings.NonInteractive))
            {
                return ValidationResult.Error("--list cannot be combined with authentication options");
            }

            return ValidationResult.Success();
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            if (settings.List)
            {
                var accounts = EntraIdHelper.GetAvailableAccountsAsync().GetAwaiter().GetResult();
                WriteAccounts(accounts);
                return 0;
            }

            var metadata = AccessTokenHelper.GetAuthenticationMetadata(
                GetAuthenticationConnectionString(settings),
                settings);

            Console.WriteLine($"Account: {SanitizeOutputValue(metadata.Username)}");
            Console.WriteLine($"Tenant: {SanitizeOutputValue(metadata.TenantId)}");
            Console.WriteLine($"Expires: {FormatExpiration(metadata.ExpiresOn)}");
            return 0;
        }

        internal static string FormatExpiration(DateTimeOffset expiresOn)
        {
            return expiresOn.ToLocalTime().ToString("O");
        }

        internal static IEnumerable<string> FormatAccountLines(IReadOnlyList<AvailableEntraAccount> accounts)
        {
            var rows = (accounts ?? Array.Empty<AvailableEntraAccount>())
                .Select(account => new
                {
                    Account = SanitizeOutputValue(account.Username),
                    Tenant = SanitizeOutputValue(account.TenantId),
                    Source = account.Source == EntraAccountSource.DaxStudioCache
                    ? "DAX Studio cache"
                    : "Windows"
                })
                .ToList();

            var accountWidth = Math.Max("Account".Length, rows.Select(row => row.Account.Length).DefaultIfEmpty().Max());
            var tenantWidth = Math.Max("Tenant".Length, rows.Select(row => row.Tenant.Length).DefaultIfEmpty().Max());

            yield return $"{"Account".PadRight(accountWidth)}  {"Tenant".PadRight(tenantWidth)}  Source";

            foreach (var row in rows)
                yield return $"{row.Account.PadRight(accountWidth)}  {row.Tenant.PadRight(tenantWidth)}  {row.Source}";
        }

        private static void WriteAccounts(IReadOnlyList<AvailableEntraAccount> accounts)
        {
            foreach (var line in FormatAccountLines(accounts))
                Console.WriteLine(line);
        }

        internal static string SanitizeOutputValue(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ');
        }
    }
}