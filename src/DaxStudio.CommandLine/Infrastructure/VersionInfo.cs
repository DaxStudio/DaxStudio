using Spectre.Console;
using System.Reflection;

namespace DaxStudio.CommandLine.Infrastructure
{

    internal class VersionInfo
    {
        internal static void Output()
        {
            string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            AnsiConsole.MarkupLine($"DSCMD - [deepskyblue1]DAX Studio[/] Commandline [underline]PREVIEW[/] ({assemblyVersion})");
        }
    }
}
