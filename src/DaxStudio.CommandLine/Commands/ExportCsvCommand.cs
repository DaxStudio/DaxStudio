using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using DaxStudio.CommandLine.Converters;
using DaxStudio.CommandLine.Extensions;
using DaxStudio.CommandLine.Helpers;
using DaxStudio.CommandLine.UIStubs;
using DaxStudio.Core.Connections;
using DaxStudio.Core.Exports;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;


namespace DaxStudio.CommandLine.Commands
{
    internal class ExportCsvCommand : AsyncCommand<ExportCsvCommand.Settings>
    {
        internal class Settings: CommandSettingsFolderBase
        {

            //[CommandArgument(0,"[folder]")]
            //[Description("The folder where the csv files will be written")]
            //public string Folder { get; set; }

            [TypeConverter(typeof(StringListTypeConverter))]
            [CommandOption("-t|--tables <tables>")]
            [Description("A list of tables to be exported, if this option is not specified all the tables in the model are exported")]
            public List<string> Tables { get; set; }

            
            
        }

        static List<SelectedTable> SelectedTables = new List<SelectedTable>();
        static List<ProgressTask> ProgressTasks = new List<ProgressTask>();
        static IEventAggregator EventAggregator;
        public ExportCsvCommand(IEventAggregator eventAggregator)
        {
            EventAggregator = eventAggregator;
        }

        public ValidationResult Validate(CommandContext context, CommandSettings settings)
        {
            return base.Validate(context, (Settings)settings);
        }

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            
            Log.Information("Starting EXPORT CSV command");
            var HasError = false;
            //AnsiConsole.MarkupLine("Starting [yellow]EXPORTCSV[/] Command...");

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
                        var connStr = settings.FullConnectionString;

                        var connEvent = new ConnectEvent()
                        {
                            ConnectionString = connStr,
                            ApplicationName = "DAX Studio Command Line",
                            DatabaseName = settings.Database,
                            PowerBIFileName = "",
                            AccessToken = AccessTokenHelper.IsAccessTokenNeeded(connStr)
                                ? AccessTokenHelper.GetAccessToken(connStr, settings)
                                : default
                        };
                        connMgr.Connect(connEvent);
                        connMgr.SelectedModel = connMgr.Database.Models.BaseModel;
                        connMgr.SelectedModelName = connMgr.SelectedModel.Name;
                        Log.Information($"Connected to Tabular Server: {settings.Server}");
                        var metadataPane = new CmdLineMetadataPane();
                        var doc = new CmdLineDocument(connMgr, metadataPane);
                        var vm = new ExportDataWizardModel(EventAggregator, doc);


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
                            ProgressTasks.Add(tsk);
                            cnt++;
                        }
                        Log.Information($"Exporting {SelectedTables.Count} table{(SelectedTables.Count > 1 ? "s" : "")} to CSV files");
                        await vm.ExportDataToCsvFilesAsync(settings.OutputFolder, SelectedTables);
                        connMgr.Close();
                        Log.Information("{0}", "Done!");
                    }
                    catch(AggregateException aex)
                    {
                        foreach( var ex in aex.InnerExceptions)
                        {
                            Log.Error(ex, "Error: {message}",ex.Message);
                        }
                        HasError = true;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex,"Error: {message}",ex.Message);
                        HasError = true;
                    }
                }).ConfigureAwait(false);

            if (HasError) return 1;
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
                    if (SelectedTables[i].Status == ExportStatus.Exporting)
                    {
                        ProgressTasks[i].IsIndeterminate(false);
                        ProgressTasks[i].StartTask();
                    }
                }
            }
        }

        
    }
}
