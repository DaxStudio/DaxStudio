using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine.Commands
{
    internal class CommandSettingsFolderBase: CommandSettingsBase
    {
        [CommandArgument(0, "<OutputFolder>")]
        public string OutputFolder { get; set; }
    }
}
