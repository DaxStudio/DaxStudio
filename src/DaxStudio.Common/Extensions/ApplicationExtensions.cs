using Fclp;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DaxStudio.Common.Extensions
{
    public static class ApplicationExtensions
    {
        public static void ReadCommandLineArgs(this Application app, string[] args)
        {
            app.Args().Clear();

            var p = new FluentCommandLineParser();
            p.Setup<int>('p', "port")
                .Callback(port => app.Args().Port = port);

            p.Setup<bool>('l', "log")
                .Callback(log => app.Args().LoggingEnabledByCommandLine = log)
                .WithDescription("Enable Debug Logging")
                .SetDefault(false);

            p.Setup<string>('f', "file")
                .Callback(file => app.Args().FileName = file)
                .WithDescription("Name of file to open");
#if DEBUG
            // only include the crashtest parameter on debug builds
            p.Setup<bool>('c', "crashtest")
                .Callback(crashTest => app.Args().TriggerCrashTest = crashTest)
                .SetDefault(false);
#endif
            p.Setup<string>('s', "server")
                .Callback(server => app.Args().Server = server)
                .WithDescription("Server to connect to");

            p.Setup<string>('d', "database")
                .Callback(database => app.Args().Database = database)
                .WithDescription("Database to connect to");

            p.Setup<bool>('r', "reset")
                .Callback(reset => app.Args().Reset = reset)
                .WithDescription("Reset user preferences to the default settings");

            p.Setup<bool>("nopreview")
                .Callback(nopreview => app.Args().NoPreview = nopreview)
                .WithDescription("Hides version information");

            p.Setup<string>('u', "uri")
                .Callback(uri => app.Args().ParseUri( uri))
                .WithDescription("used by the daxstudio:// uri handler");


            p.SetupHelp("?", "help")
                .Callback(text => {
                    Log.Information(Constants.LogMessageTemplate, nameof(ApplicationExtensions), nameof(ReadCommandLineArgs), "Printing CommandLine Help");
                    Version ver = Assembly.GetExecutingAssembly().GetName().Version;
                    string formattedHelp = HelpFormatter.Format(p.Options);
                    Console.WriteLine("");
                    Console.WriteLine($"DAX Studio {ver.ToString(3)} (build {ver.Revision})");
                    Console.WriteLine("--------------------------------");
                    Console.WriteLine("");
                    Console.WriteLine("Supported command line parameters:");
                    Console.WriteLine(formattedHelp);
                    Console.WriteLine("");
                    Console.WriteLine("Note: parameters can either be passed with a short name or a long name form");
                    Console.WriteLine("eg.  DaxStudio -f myfile.dax");
                    Console.WriteLine("     DaxStudio --file myfile.dax");
                    Console.WriteLine("");
                    //app.Args().HelpText = text;
                    app.Args().ShowHelp = true;
                });

            p.Parse(args);

        }
    }
}
