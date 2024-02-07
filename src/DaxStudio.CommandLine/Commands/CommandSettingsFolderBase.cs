using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine.Commands
{
    internal class CommandSettingsFolderBase: CommandSettingsBase
    {
        [CommandArgument(0, "<OutputFolder>")]
        [Description("The folder where the output will be written")]
        public string OutputFolder { get; set; }
    }
}
