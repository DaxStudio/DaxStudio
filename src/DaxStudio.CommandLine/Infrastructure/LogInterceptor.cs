using DaxStudio.CommandLine.Commands;
using Serilog.Core;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine.Infrastructure
{
    //internal class LogInterceptor : ICommandInterceptor
    //{
    //    public void Intercept(CommandContext context, CommandSettings settings)
    //    {
    //        if (settings is CommandSettingsBase baseSettings)
    //        {
    //            LogEnricher.Path = baseSettings.LogFile;
    //            LogEnricher.MinimumLevel = baseSettings.LogLevel;
    //        }
    //    }

    //    public void InterceptResult(CommandContext context, CommandSettings settings, ref int result)
    //    {
    //        // do nothing with the result
    //    }
    //}
}
