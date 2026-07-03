using System;
using Spectre.Console.Cli;

namespace DaxStudio.Common.Cli
{
    /// <summary>
    /// Default Spectre.Console.Cli command for the WPF launcher.
    /// Copies the parsed settings onto the <see cref="CmdLineArgs"/> instance
    /// stored in <see cref="LaunchContext.Current"/>.
    /// </summary>
    internal sealed class LaunchCommand : Command<LaunchSettings>
    {
        protected override int Execute(CommandContext context, LaunchSettings settings, System.Threading.CancellationToken cancellationToken)
        {
            var args = LaunchContext.Current;
            if (args == null)
            {
                return 0;
            }

            if (settings.Port.HasValue)
            {
                args.Port = settings.Port.Value;
            }
            if (settings.FileName != null)
            {
                args.FileName = settings.FileName;
            }
            if (settings.Server != null)
            {
                args.Server = settings.Server;
            }
            if (settings.Database != null)
            {
                args.Database = settings.Database;
            }
            if (settings.Log != null && settings.Log.IsSet)
            {
                args.LoggingEnabledByCommandLine = settings.Log.Value;
            }
            if (settings.Reset != null && settings.Reset.IsSet)
            {
                args.Reset = settings.Reset.Value;
            }
            if (settings.NoPreview != null && settings.NoPreview.IsSet)
            {
                args.NoPreview = settings.NoPreview.Value;
            }
            if (!string.IsNullOrEmpty(settings.Uri))
            {
                args.ParseUri(settings.Uri);
            }
#if DEBUG
            if (settings.CrashTest != null && settings.CrashTest.IsSet)
            {
                args.TriggerCrashTest = settings.CrashTest.Value;
            }
#endif

            return 0;
        }
    }

    /// <summary>
    /// Carrier for the <see cref="CmdLineArgs"/> instance currently being
    /// populated by <see cref="LaunchCommand"/>. The value is scoped per-thread
    /// for the duration of a single <see cref="CmdLineArgs.Parse(string[])"/>
    /// call.
    /// </summary>
    internal static class LaunchContext
    {
        [ThreadStatic]
        private static CmdLineArgs _current;

        public static CmdLineArgs Current => _current;

        public static IDisposable Use(CmdLineArgs args)
        {
            _current = args;
            return new Scope();
        }

        private sealed class Scope : IDisposable
        {
            public void Dispose() => _current = null;
        }
    }
}
