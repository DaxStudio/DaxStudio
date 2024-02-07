using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using DaxStudio.CommandLine.Extensions;
using DaxStudio.CommandLine.Infrastructure;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;


namespace DaxStudio.CommandLine.Commands
{
    internal class ExportSqlCommand : AsyncCommand<ExportSqlCommand.Settings>
    {
        internal class Settings: CommandSettingsBase
        {


            [CommandArgument(0,"[SqlConnectionString]")]
            [Description("The connection string for the SQL Server destination")]
            public string SqlConnectionString { get; set; }

            [CommandOption("-t|--tables <tables>")]
            [Description("A list of tables to be exported, if this option is not specified all the tables in the model are exported")]
            public List<string> Tables { get; set; }

            [CommandOption("-e|--schema <schema>")]
            [DefaultValue("dbo")]
            [Description("The schema in which the destination tables belong")]
            public string Schema { get; set; }

            [CommandOption("-r|--recreate-tables")]
            public bool ReCreateTables { get; set; }

            
        }

        static List<SelectedTable> SelectedTables = new List<SelectedTable>();
        static List<ProgressTask> ProgressTasks = new List<ProgressTask>();
        static IEventAggregator EventAggregator;
        public Guid UniqueId = new Guid();
        public ExportSqlCommand(IEventAggregator eventAggregator)
        {
            EventAggregator = eventAggregator;
        }

        public ValidationResult Validate(CommandContext context, CommandSettings settings)
        {
            return ValidationResult.Success();
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            VersionInfo.Output();
            Log.Information("Starting EXPORT SQL Command");
            var hasError = false;
            AnsiConsole.MarkupLine("Starting [yellow]EXPORTSQL[/] Command...");

            // Show progress
            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),    // Task description
                    new ProgressBarColumn(),        // Progress bar
                    new PercentageColumn(),         // Percentage
                    new RemainingTimeColumn(),      // Remaining time
                    new SpinnerColumn( Spinner.Known.BouncingBar ),            // Spinner
                })
                .StartAsync(async ctx =>
                {
                    try
                    {
                        var connMgr = new ConnectionManager(EventAggregator);
                        var connEvent = new ConnectEvent()
                        {
                            ConnectionString = $"Data Source={settings.Server};Initial Catalog={settings.Database}",
                            ApplicationName = "DAX Studio Command Line",
                            DatabaseName = settings.Database
                        };
                        connMgr.Connect(connEvent);
                        connMgr.SelectedModel = connMgr.Database.Models.BaseModel;
                        connMgr.SelectedModelName = connMgr.SelectedModel.Name;
                        WriteLogMessage($"Connected to Tabular Server: {settings.Server}");
                        var metadataPane = new CmdLineMetadataPane();
                        var doc = new CmdLineDocument(connMgr, metadataPane);
                        var vm = new ExportDataWizardViewModel(EventAggregator, doc, null);
                        vm.ExportType = UI.Enums.ExportDataType.SqlTables;


                        var tables = settings.Tables;
                        if (tables == null)
                        {
                            tables = new List<string>();
                            foreach (var t in connMgr.SelectedModel.Tables)
                            {
                                tables.Add(t.Name);
                            }
                        }
                        int cnt = 0;
                        foreach (var t in tables)
                        {
                            var tbl = new SelectedTable(t.ToDaxName(), t, true, false, false);
                            tbl.PropertyChanged += OnPropertyChanged;
                            SelectedTables.Add(tbl);

                            var tsk = ctx.AddTask(t, false, 1);
                            tsk.IsIndeterminate();
                            ProgressTasks.Add(tsk);
                            cnt++;
                        }
                        WriteLogMessage($"Exporting {SelectedTables.Count} table{(SelectedTables.Count > 1 ? "s" : "")} to SQL Server");
                        await vm.ExportDataToSqlTables(settings.Schema, settings.ReCreateTables, settings.SqlConnectionString, SelectedTables, connMgr);
                        connMgr.Close();
                        AnsiConsole.MarkupLine("[green]Done![/]");

                    }
                    catch (AggregateException aex)
                    {
                        foreach(var ex in aex.InnerExceptions)
                        {
                            Log.Error(ex, "Error: {message}", ex.Message);
                            hasError = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error: {message}", ex.Message);
                        hasError = true;
                    }
                });

            if (hasError) return 1;
            else return 0;
        }

        private static void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            for (var i = 0; i < SelectedTables.Count; i++)
            {
                if (SelectedTables[i] == sender)
                {
                    ProgressTasks[i].MaxValue = SelectedTables[i].TotalRows;
                    ProgressTasks[i].Value = SelectedTables[i].RowCount;
                    if (SelectedTables[i].Status == UI.Enums.ExportStatus.Exporting)
                    {
                        ProgressTasks[i].IsIndeterminate(false);
                        ProgressTasks[i].StartTask();
                    }
                }
            }
        }

        private static void WriteLogMessage(string message)
        {
            AnsiConsole.MarkupLine(
                "[grey]LOG:[/] " +
                message +
                "[grey]...[/]");
        }
    }
}
