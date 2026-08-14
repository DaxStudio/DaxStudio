using DaxStudio.CommandLine.Helpers;
using DaxStudio.Common;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Threading;

namespace DaxStudio.CommandLine.Commands
{
    /// <summary>
    /// Signs in once, interactively, so that the account is cached and every later command can
    /// acquire its token silently. This is the bootstrap step that makes unattended batches possible
    /// on a machine with more than one account.
    /// </summary>
    internal class AuthCommand : Command<AuthCommand.Settings>
    {
        internal class Settings : CommandSettingsRawBase
        {
            [Spectre.Console.Cli.CommandOption("--check")]
            [System.ComponentModel.Description("Only check whether a token can be acquired silently; never prompt. Use this to verify an unattended job will run before scheduling it")]
            public bool CheckOnly { get; set; }
        }

        protected override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.Server) && string.IsNullOrWhiteSpace(settings.ConnectionString))
                return ValidationResult.Error("You must specify a <server> or a <connectionstring> so that the correct tenant and scope can be used");

            return ValidationResult.Success();
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var connStr = settings.FullConnectionString;

            if (!AccessTokenHelper.IsAccessTokenNeeded(connStr))
            {
                AnsiConsole.MarkupLine("[yellow]This connection does not use Entra authentication, so no sign-in is required.[/]");
                return 0;
            }

            // --check must never prompt: its whole purpose is to answer "would an unattended run
            // succeed right now?". Without it, this command is allowed to prompt because signing in
            // interactively is exactly what it is for.
            var nonInteractive = settings.CheckOnly || settings.IsNonInteractive;

            try
            {
                var token = AccessTokenHelper.GetAccessToken(connStr, settings.ResolvedUserID, nonInteractive);
                var username = (token.UserContext as AccessTokenContext)?.Username;

                AnsiConsole.MarkupLine(settings.CheckOnly
                    ? $"[green]OK[/] a token was acquired silently for [blue]{Markup.Escape(username ?? "(unknown account)")}[/]"
                    : $"[green]Signed in[/] as [blue]{Markup.Escape(username ?? "(unknown account)")}[/]");
                AnsiConsole.MarkupLine($"Token expires at [blue]{token.ExpirationTime.ToLocalTime():g}[/]");

                if (!settings.CheckOnly && !string.IsNullOrWhiteSpace(username))
                {
                    AnsiConsole.MarkupLine($"Unattended commands should now pass [blue]-u \"{Markup.Escape(username)}\"[/] (or set DSCMD_USER) to select this account.");
                }

                return 0;
            }
            catch (EntraAuthenticationException ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(AuthCommand), nameof(Execute), "Authentication failed");
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
                return 1;
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(AuthCommand), nameof(Execute), "Authentication failed");
                AnsiConsole.MarkupLine($"[red]Authentication failed: {Markup.Escape(ex.Message)}[/]");
                return 1;
            }
        }
    }
}
