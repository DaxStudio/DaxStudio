using Spectre.Console.Cli.Help;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using System.Collections.Generic;
using Spectre.Console;
using System.Windows;
using System;
using System.Reflection;

namespace DaxStudio.CommandLine
{
    internal class CustomHelpProvider : HelpProvider
    {
        public CustomHelpProvider(ICommandAppSettings settings)
            : base(settings)
        {
        }

        public override IEnumerable<IRenderable> GetHeader(ICommandModel model, ICommandInfo? command)
        {
            Version appVersion = Assembly.GetExecutingAssembly().GetName().Version;
            string version = appVersion.ToString(3);

            //Markup.FromInterpolated($"[bold yellow]DSCMD[/] v{version}", null), Text.NewLine,
            // new FigletText("dscmd"),
            return new IRenderable[]
            {
                new Markup("[bold dodgerblue1]DSCMD[/]  "), new Text($"v{version}"), new Markup("  [dim link]https://daxstudio.org[/]"), Text.NewLine,
                new Markup("[dim]DAX Studio command line utility[/]"), Text.NewLine,
                Text.NewLine
            };
        }
    }
}
