using Fclp.Internals.Extensions;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Data.OleDb;

namespace DaxStudio.CommandLine.Commands
{
    internal abstract class CommandSettingsBase: CommandSettings
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


        [CommandArgument(1,"[connectionstring]")]
        [CommandOption("-c|--connectionstring <connectionString>")]
        [Description("The connection string for the data source")]
        public string ConnectionString { get; set; }

        public override ValidationResult Validate()
        {
            if (!string.IsNullOrWhiteSpace(ConnectionString)
                && (!string.IsNullOrWhiteSpace(Server) 
                    || !string.IsNullOrWhiteSpace(Database)))
                { return ValidationResult.Error("You cannot specify a <Server> or <Database> when passing a <ConnectionString>"); }

            if (string.IsNullOrEmpty(ConnectionString) && !string.IsNullOrWhiteSpace(Server) && string.IsNullOrWhiteSpace(Database))
                return ValidationResult.Error("You must specify a <database> when using the <server> parameter");

            if (string.IsNullOrEmpty(ConnectionString) && !string.IsNullOrWhiteSpace(Database) && string.IsNullOrWhiteSpace(Server))
                return ValidationResult.Error("You must specify a <server> when using the <database> parameter");

            if (!string.IsNullOrWhiteSpace(UserID) && string.IsNullOrWhiteSpace(Password))
                { return ValidationResult.Error("You must specify a <Password> when passing a <UserID>"); }

            if (!string.IsNullOrWhiteSpace(Password) && string.IsNullOrWhiteSpace(UserID))
            { return ValidationResult.Error("You must specify a <UserID> when passing a <Password>"); }

            return base.Validate();
        }

    }
}
