using DaxStudio.CommandLine.Interfaces;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Data.OleDb;

namespace DaxStudio.CommandLine.Commands
{
    internal abstract class CommandSettingsBase : CommandSettings, ISettingsConnection
    {

        [CommandOption("-s|--server <server>")]
        [Description("The name of the tabular server to connect to")]
        public string Server { get; set; }

        [CommandOption("-d|--database <database>")]
        [Description("The name of the tabular database to export")]
        public string Database { get; set; }

        [CommandOption("-u|--userid <userid>")]
        [Description("The username to use for AzureAD authentication")]
        public string UserID { get; set; }

        [CommandOption("-p|--password <password>")]
        [Description("The password to use for AzureAD authentication")]
        public string Password { get; set; }


        [CommandArgument(1, "[connectionstring]")]
        [CommandOption("-c|--connectionstring <connectionString>")]
        [Description("The connection string for the data source")]
        public string ConnectionString { get; set; }

        public string PowerBIFileName { get; set; }

        public string FullConnectionString { get {
                
                string user = GetPropertyOrEnvironmentVariable(nameof(UserID), UserID, "DSCMD_USER");
                string pass = GetPropertyOrEnvironmentVariable(nameof(Password), Password, "DSCMD_PASSWORD");

                string userParam = !string.IsNullOrEmpty(user) ? $"User ID={user};":string.Empty;
                string passParam = !string.IsNullOrEmpty(pass) ? $"Password={pass};" : string.Empty;

                // if the connectionstring property is set use that
                if (string.IsNullOrEmpty(ConnectionString)) 
                    return $"Data Source={Server};Initial Catalog={Database};{userParam}{passParam}";

                var builder = new OleDbConnectionStringBuilder(ConnectionString);
                if (!builder.ContainsKey("User ID") && !string.IsNullOrEmpty(user))
                    builder["User ID"] = user;
                if (!builder.ContainsKey("Password") && !builder.ContainsKey("Pwd") && !string.IsNullOrEmpty(user))
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

            if (!string.IsNullOrWhiteSpace(UserID) && string.IsNullOrWhiteSpace(Password))
                { return ValidationResult.Error("You must specify a <Password> when passing a <UserID>"); }

            if (!string.IsNullOrWhiteSpace(Password) && string.IsNullOrWhiteSpace(UserID))
            { return ValidationResult.Error("You must specify a <UserID> when passing a <Password>"); }

            return base.Validate();
        }

        internal void CheckForDesktopConnection()
        {
            if (!(Server.EndsWith(".pbix", StringComparison.OrdinalIgnoreCase)
                || Server.EndsWith(".pbip", StringComparison.OrdinalIgnoreCase))) return;

            Log.Information("Detected Power BI Desktop file");

            PowerBIFileName = Server.Substring(0,Server.Length-5);
            AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.Star)
                .SpinnerStyle(Style.Parse("green bold"))
                .Start("Scanning for running instances of Power BI Desktop...", ctx =>
                {
                    var instances = UI.Utils.PowerBIHelper.GetLocalInstances(false);
                
                    foreach (var instance in instances)
                    {
                        if (instance.Name == PowerBIFileName)
                        {
                            Server = $"localhost:{instance.Port}";
                            Log.Information($"Found running instance of '{PowerBIFileName}' on port: {instance.Port}");
                            return;
                        }
                    }
                });
        }

    }
}
