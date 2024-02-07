using Caliburn.Micro;
using DaxStudio.CommandLine.ViewModel;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine.Commands
{
    internal class CustomTraceCommand : AsyncCommand<CustomTraceCommand.Settings>
    {
        internal class Settings : CommandSettingsBase
        {
            [CommandArgument(0, "[Template]")]
            [Description("The trace template to be used")]
            public string TemplateName { get; set; }

            [CommandArgument(1, "[file]")]
            [Description("The file name where trace events should be recorded")]
            public string File { get; set; }

            public CustomTraceTemplate Template { get; set; }

        }

        public IEventAggregator EventAggregator { get; }
        public IGlobalOptions Options { get; }
        public IWindowManager WindowManager { get; }
        public SortedList<string, CustomTraceTemplate> Templates { get; }
        private static readonly CancellationTokenSource canToken = new CancellationTokenSource();

        public CustomTraceCommand(IEventAggregator eventAggregator, IGlobalOptions options)
        {
            EventAggregator = eventAggregator;
            Options = options;
            WindowManager = null;
            var dialog = new CustomTraceDialogViewModel(Options);
            Templates = dialog.Templates;
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            var mySettings = settings as CustomTraceCommand.Settings;
            // Check if this is a known template
            if (!Templates.ContainsKey(settings.TemplateName))
            {
                return ValidationResult.Error($"'{settings.TemplateName}' is not a valid template. It should be one of [{string.Join(",", Templates.Keys.ToArray())}]");
            }
            else
            {
                settings.Template = Templates[settings.TemplateName];
            }

            // todo check if the file path is valid

            return base.Validate(context, settings);
        }
        private StatusContext statusContext;
        private static CustomTraceViewModel customTracer;

        public override async Task<int> ExecuteAsync(CommandContext context, CustomTraceCommand.Settings settings)
        {
            Log.Information("Starting [yellow]Custom Trace[/] Command");

            //AnsiConsole.MarkupLine("Starting [yellow]EXPORTCSV[/] Command...");


            // Show progress
            var progress = AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),    // Task description
                    new ProgressBarColumn(),        // Progress bar
                    new PercentageColumn(),         // Percentage
                    new RemainingTimeColumn(),      // Remaining time
                    new SpinnerColumn( Spinner.Known.BouncingBar ),            // Spinner
                });

            var status = AnsiConsole.Status();
            
            

            await AnsiConsole.Status().StartAsync("Starting Trace...", async ctx =>
                {
                    statusContext = ctx;
                    var connMgr = new ConnectionManager(EventAggregator);
                    var connEvent = new ConnectEvent()
                    {
                        ConnectionString = $"Data Source={settings.Server};Initial Catalog={settings.Database}",
                        ApplicationName = "DAX Studio Command Line",
                        DatabaseName = settings.Database,
                        PowerBIFileName = ""
                    };
                try {
                    connMgr.Connect(connEvent);
                    connMgr.SelectedModel = connMgr.Database.Models.BaseModel;
                    connMgr.SelectedModelName = connMgr.SelectedModel.Name;
                    Log.Information($"Connected to Tabular Server: {settings.Server}");
                    var metadataPane = new CmdLineMetadataPane();
                    var doc = new CmdLineDocument(connMgr, metadataPane);

                    customTracer = new CmdCustomTraceViewModel(EventAggregator, Options, WindowManager);
                    customTracer.PropertyChanged += OnPropertyChanged;
                    customTracer.Document = doc;
                    customTracer.Template = settings.Template;
                    customTracer.OutputFile = settings.File;
                    customTracer.SetTraceOutput(UI.Enums.CustomTraceOutput.FileAndGrid);
                    customTracer.IsChecked = true;

                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error");
                    }

                    Console.CancelKeyPress += (sender, eventArgs) => {
                        Console.WriteLine("Cancel event triggered");
                        canToken.Cancel();
                        eventArgs.Cancel = true;
                    };

                    // waits for Ctrl+C
                    await Worker();

                    // wait for trace to start
                    // display grid
                    // display event count
                    // cancel trace on ctrl-c
                    Log.Information("Stopping Trace");

                    await customTracer?.StopTraceAsync();
                    connMgr?.Close();
                });
            Log.Information("{0}", "Done!");

            return 0;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var vm = sender as CustomTraceViewModel;
            if (vm == null) return;

            switch (e.PropertyName)
            {
                case "EventCount":
                    // todo update event count
                    statusContext.Status($"EventCount: {vm.EventCount}  File: {vm.OutputFile}");
                    break;
            }
        }

        async static Task Worker()
        {
            while (!canToken.IsCancellationRequested)
            {
                // do work       
                //Console.WriteLine("Worker is working");
                
                await Task.Delay(1000); // arbitrary delay
            }
            
        }

    }
}
