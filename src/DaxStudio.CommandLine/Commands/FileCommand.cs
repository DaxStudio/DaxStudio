using Serilog;
using Spectre.Console.Cli;
using System.ComponentModel;
using Spectre.Console;
using DaxStudio.UI.Interfaces;
using System.Collections.Generic;
using Microsoft.AnalysisServices.AdomdClient;
using DaxStudio.UI.Model;
using System;
using System.IO;
using DaxStudio.Interfaces.Enums;
using DaxStudio.CommandLine.UIStubs;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine.Commands
{
    internal class FileCommand : AsyncCommand<FileCommand.Settings>
    {
        internal class Settings : CommandSettingsFileBase,IQueryTextProvider
        {

            [CommandOption("-f|--file <file>")]
            [Description("A text file containing a DAX query to be executed")]
            public string File { get; set; }

            [CommandOption("-q|--query <query>")]
            [Description("A DAX query to be executed")]
            public string Query { get; set; }

            [CommandOption("-t|--fileType")]
            [Description("Specifies the format of the file")]
            public TextFileType FileType { get; set; }

            public string EditorText => Query;

            public string QueryText => Query;

            [CommandOption("-m|--parameter <PARAMETER=VALUE>")]
            public IDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

            private List<AdomdParameter> _parameters = new List<AdomdParameter>();
            public List<AdomdParameter> ParameterCollection { get 
                {
                    if (_parameters.Count == 0 && Parameters.Count > 0)
                    {
                        foreach (var p in Parameters)
                        {
                            Log.Information("Setting parameter {name} to {value}", p.Key, p.Value);
                            // TODO - should we try to parse the value to see if it is an int or double or datetime?
                            _parameters.Add(new AdomdParameter(p.Key, p.Value));
                        }
                    }
                    return _parameters;
                } 
            } 
            public QueryInfo QueryInfo { get => new QueryInfo(Query, null); set => throw new NotImplementedException(); }


        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            
            if (string.IsNullOrWhiteSpace(settings.OutputFile)) return ValidationResult.Error("You must specify an Out option");
            var result = base.Validate(context, settings);
            return result;
        }

        

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            
            Log.Information("Starting File command");

            if (settings.File != null && settings.Query == null)
            {
                settings.Query = File.ReadAllText(settings.File);
            }

            QueryRunner runner = new QueryRunner(settings);
            var target = new DaxStudio.UI.ResultsTargets.ResultsTargetTextFile();

            if (settings.FileType == TextFileType.Unknown)
            {
                var fi = new FileInfo(settings.OutputFile);

                switch (fi.Extension.ToLower())
                {
                    case ".csv":
                        settings.FileType = TextFileType.UTF8CSV;
                        break;
                    case ".txt":
                        settings.FileType = TextFileType.TAB;
                        break;
                    case ".json":
                        settings.FileType = TextFileType.JSON;
                        break;
                    case ".parquet":
                        settings.FileType = TextFileType.PARQUET;
                        break;
                    default:
                        settings.FileType = (TextFileType)runner.Options.DefaultTextFileType;
                        break;
                }
            }

            // export to csv
            await AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.Star)
                .SpinnerStyle(Style.Parse("green bold"))
                .StartAsync("Exporting to file...", async ctx =>
                {
                    //AnsiConsole.MarkupLine("[green]Done![/]");

                    runner.Options.CmdLineTextFileType = settings.FileType;
                    await target.OutputResultsAsync(runner, settings, settings.OutputFile).ConfigureAwait(true);
                });

            Log.Information("Finished FILE command");
            return 0;

        }
    }

}
