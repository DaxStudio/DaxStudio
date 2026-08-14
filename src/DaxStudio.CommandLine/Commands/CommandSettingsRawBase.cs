using DaxStudio.CommandLine.Interfaces;
using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using DaxStudio.Core.Utils;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Data.OleDb;

namespace DaxStudio.CommandLine.Commands
{
    internal abstract class CommandSettingsRawBase : CommandSettings, ISettingsConnection
    {

        [CommandOption("-s|--server <server>")]
        [Description("The name of the tabular server to connect to. If you use <filename>.pbix or <filename>.pbip the command will look for a running instance of Power BI desktop that has that file open")]
        public string Server { get; set; }

        [CommandOption("-d|--database <database>")]
        [Description("The name of the tabular database to export")]
        public string Database { get; set; }

        [CommandOption("-u|--userid <userid>")]
        [Description("The account to authenticate as. With --password this is a username; without it, this selects which cached Entra account to sign in with, which is what keeps unattended runs on a multi-account machine from prompting. Can also be set with the DSCMD_USER environment variable")]
        public string UserID { get; set; }

        [CommandOption("-p|--password <password>")]
        [Description("The password to use for AzureAD authentication. Can also be set with the DSCMD_PASSWORD environment variable")]
        public string Password { get; set; }

        [CommandOption("--non-interactive")]
        [Description("Never prompt for an interactive sign-in. If a token cannot be acquired silently the command fails with an error explaining how to cache the account. Can also be set with the DSCMD_NON_INTERACTIVE environment variable")]
        public bool NonInteractive { get; set; }




        //[CommandArgument(1, "[connectionstring]")]
        [CommandOption("-c|--connectionstring <connectionString>")]
        [Description("The connection string for the data source")]
        public string ConnectionString { get; set; }

        public string PowerBIFileName { get; set; }

        private string _resolvedUserID;
        private string _resolvedPassword;
        private bool? _isNonInteractive;

        /// <summary>
        /// The account dscmd should authenticate as, taken from -u|--userid, DSCMD_USER, or a User ID
        /// already present on a supplied --connectionstring. This selects the Entra account and is
        /// deliberately independent of any persisted UI setting, so that concurrent dscmd processes
        /// each authenticate as exactly the identity they were told to use.
        /// </summary>
        /// <remarks>
        /// Resolved once per command. Several commands read this and the connection string
        /// separately, and re-resolving would repeat the "Using ... argument" log line each time.
        /// </remarks>
        public string ResolvedUserID => _resolvedUserID ?? (_resolvedUserID = ResolveUserID());

        private string ResolveUserID()
        {
            var user = GetPropertyOrEnvironmentVariable(nameof(UserID), UserID, "DSCMD_USER");
            if (!string.IsNullOrEmpty(user)) return user;

            // A User ID on a supplied connection string is also an account selector, and
            // FullConnectionString strips it for token-based connections, so it has to be captured
            // here or the caller's choice of account would be silently discarded.
            if (!string.IsNullOrEmpty(ConnectionString))
            {
                var supplied = new OleDbConnectionStringBuilder(ConnectionString);
                if (supplied.ContainsKey("User ID")) return supplied["User ID"]?.ToString() ?? string.Empty;
                if (supplied.ContainsKey("UID")) return supplied["UID"]?.ToString() ?? string.Empty;
            }

            return string.Empty;
        }

        private string ResolvedPassword => _resolvedPassword
            ?? (_resolvedPassword = GetPropertyOrEnvironmentVariable(nameof(Password), Password, "DSCMD_PASSWORD"));

        /// <summary>
        /// True when the command must not block on a sign-in prompt. Set explicitly with
        /// --non-interactive / DSCMD_NON_INTERACTIVE, and inferred when the process has no way to
        /// display a dialog at all.
        /// </summary>
        public bool IsNonInteractive => _isNonInteractive ?? (bool)(_isNonInteractive = ResolveNonInteractive());

        private bool ResolveNonInteractive()
        {
            if (NonInteractive) return true;

            var variable = Environment.GetEnvironmentVariable("DSCMD_NON_INTERACTIVE");
            if (!string.IsNullOrEmpty(variable)
                && (variable.Equals("1", StringComparison.OrdinalIgnoreCase)
                    || variable.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || variable.Equals("yes", StringComparison.OrdinalIgnoreCase)))
            {
                Log.Information("Using environment variable DSCMD_NON_INTERACTIVE for {propertyName}", nameof(NonInteractive));
                return true;
            }

            // Safety net only. A process running as a service or in session 0 cannot show the
            // WAM dialog at all, so prompting would hang rather than fail. This deliberately
            // does NOT catch the common "interactive session but nobody is watching" case
            // (scheduled/agent-driven runs) - only the caller can declare that.
            if (!Environment.UserInteractive || Helpers.NativeMethods.GetConsoleWindow() == IntPtr.Zero)
            {
                Log.Information(Constants.LogMessageTemplate, nameof(CommandSettingsRawBase), nameof(IsNonInteractive),
                    "No interactive window is available, treating this command as non-interactive");
                return true;
            }

            return false;
        }

        public string FullConnectionString { get {

                string user = ResolvedUserID;
                string pass = ResolvedPassword;

                // Always build the connection string through OleDbConnectionStringBuilder
                // so that values containing special characters (';', '=', '"', leading/
                // trailing whitespace) are quoted correctly and any embedded single
                // quotes are doubled per the connection-string grammar.
                var builder = string.IsNullOrEmpty(ConnectionString)
                    ? new OleDbConnectionStringBuilder()
                    : new OleDbConnectionStringBuilder(ConnectionString);

                if (string.IsNullOrEmpty(ConnectionString))
                {
                    if (!string.IsNullOrEmpty(Server)) builder["Data Source"] = Server;
                    if (!string.IsNullOrEmpty(Database)) builder["Initial Catalog"] = Database;
                }

                // Determined before either keyword is written so that the emitted key order is
                // unchanged from previous releases.
                var hasPassword = builder.ContainsKey("Password") || builder.ContainsKey("Pwd") || !string.IsNullOrEmpty(pass);

                if (!hasPassword && builder.DataSource.RequiresEntraAuth())
                {
                    // Without a password the user id names which cached Entra account to get a
                    // token for; it is not a credential. Leaving it on the connection string makes
                    // MSOLAP/AMO attempt a username+password sign-in instead of using the token,
                    // which prompts for a password or, once the token is supplied as the password,
                    // fails with AADSTS50052 because a JWT is far longer than a password may be.
                    builder.Remove("User ID");
                    builder.Remove("UID");
                }
                else if (!builder.ContainsKey("User ID") && !string.IsNullOrEmpty(user))
                {
                    builder["User ID"] = user;
                }

                if (!builder.ContainsKey("Password") && !builder.ContainsKey("Pwd") && !string.IsNullOrEmpty(pass))
                    builder["Password"] = pass;

                return builder.ToString();

            } 
        }

        private string GetPropertyOrEnvironmentVariable(string propertyName, string property, string variableName)
        {
            
            string variable = Environment.GetEnvironmentVariable(variableName);
            if (!string.IsNullOrEmpty(property))
            {
                Log.Information("Using {propertyName} argument", propertyName);
                // using UserID property
                return property;
            }
            if (!string.IsNullOrEmpty(variable))
            {
                Log.Information("Using environment variable {variableName} for {propertyName}", variableName, propertyName);
                // using Environment user
                return variable;
            }
            return string.Empty;
        }

        public override ValidationResult Validate()
        {
            

            if (!string.IsNullOrWhiteSpace(ConnectionString)
                && (!string.IsNullOrWhiteSpace(Server) 
                    || !string.IsNullOrWhiteSpace(Database)))
                { return ValidationResult.Error("You cannot specify a <Server> or <Database> when passing a <ConnectionString>"); }

            if (string.IsNullOrEmpty(ConnectionString) 
            && !string.IsNullOrWhiteSpace(Server) 
            && string.IsNullOrWhiteSpace(Database) 
            && !(Server.EndsWith(".pbix", StringComparison.OrdinalIgnoreCase)
                || Server.EndsWith(".pbip", StringComparison.OrdinalIgnoreCase)))
                return ValidationResult.Error("You must specify a <database> when using the <server> parameter and not connecting to a .pbix/.pbip file");

            if (string.IsNullOrEmpty(ConnectionString) && !string.IsNullOrWhiteSpace(Database) && string.IsNullOrWhiteSpace(Server))
                return ValidationResult.Error("You must specify a <server> when using the <database> parameter");

            //if (!string.IsNullOrWhiteSpace(UserID) && string.IsNullOrWhiteSpace(Password))
            //    { return ValidationResult.Error("You must specify a <Password> when passing a <UserID>"); }

            //if (!string.IsNullOrWhiteSpace(Password) && string.IsNullOrWhiteSpace(UserID))
            //{ return ValidationResult.Error("You must specify a <UserID> when passing a <Password>"); }

            CheckForDesktopConnection();

            return base.Validate();
        }

        internal void CheckForDesktopConnection()
        {
            if (Server == null) return; // this probably means that --ConnectionString is being used

            if (!(Server.EndsWith(".pbix", StringComparison.OrdinalIgnoreCase)
                || Server.EndsWith(".pbip", StringComparison.OrdinalIgnoreCase))) return;

            PowerBIFileName = Server.Substring(0,Server.Length-5);
            AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.Star)
                .SpinnerStyle(Style.Parse("green bold"))
                .Start("Scanning for running instances of Power BI Desktop...", ctx =>
                {
                    var instances = PowerBIHelper.GetLocalInstances(false, true);
                
                    foreach (var instance in instances)
                    {
                        if (instance.Name.Equals(PowerBIFileName, StringComparison.CurrentCultureIgnoreCase))
                        {
                            Server = $"localhost:{instance.Port}";
                            Log.Information($"Found running instance of '{PowerBIFileName}' on port: {instance.Port}");
                            break;
                        }
                    }
                });

            if (!Server.StartsWith("localhost:"))
                throw new ArgumentException($"Invalid Server parameter. Unable to find a running Power BI Desktop instance with the '{Server}' file open");

        }

    }
}
