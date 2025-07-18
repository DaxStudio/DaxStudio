﻿using Serilog;
using Spectre.Console.Cli;
using System.ComponentModel;
using Spectre.Console;
using DaxStudio.UI.Interfaces;
using System.Collections.Generic;
using Microsoft.AnalysisServices.AdomdClient;
using DaxStudio.UI.Model;
using System;
using DaxStudio.CommandLine.Infrastructure;
using System.IO;
using DaxStudio.Interfaces.Enums;
using DaxStudio.CommandLine.UIStubs;
using DaxStudio.UI.Utils;
using System.Linq;

namespace DaxStudio.CommandLine.Commands
{
    internal class CsvCommand : Command<CsvCommand.Settings>
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

            //[CommandArgument(0,"[filename]")]
            //[Description("The name of the file for the results")]
            //public string Out { get; set; }

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

        public override int Execute(CommandContext context, Settings settings)
        {
            
            Log.Information("Starting CSV command");
            try
            {
                if (settings.File != null && settings.Query == null)
                {
                    settings.Query = File.ReadAllText(settings.File);
                }

                QueryRunner runner = new QueryRunner(settings);
                var target = new DaxStudio.UI.ResultsTargets.ResultsTargetTextFile();

                if (settings.FileType == TextFileType.Unknown) {
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
                        default:
                            settings.FileType = (TextFileType)runner.Options.DefaultTextFileType; 
                            break;
                    }
                }

                // export to csv
                AnsiConsole.Status()
                    .AutoRefresh(true)
                    .Spinner(Spinner.Known.Star)
                    .SpinnerStyle(Style.Parse("green bold"))
                    .Start("Exporting to file...", ctx =>
                    {
                        //AnsiConsole.MarkupLine("[green]Done![/]");

                        runner.Options.CmdLineTextFileType = settings.FileType;
                        target.OutputResultsAsync(runner, settings, settings.OutputFile).Wait();
                    });
                Log.Information("Finished CSV command");
                return 0;
            } 
            catch (AggregateException aex)
            {
                foreach (var ex in aex.InnerExceptions)
                {
                    Log.Error(ex, "Error: {message}", ex.Message);
                }
                return 2;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error: {message}", ex.Message);
                return 1;
            }
        }
    }

}
