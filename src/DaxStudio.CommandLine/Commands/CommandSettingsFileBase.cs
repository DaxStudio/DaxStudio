using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine.Commands
{
    internal class CommandSettingsFileBase: CommandSettingsBase
    {
        [CommandArgument(0, "<OutputFile>")]
        public string OutputFile { get; set; }
    }
}
