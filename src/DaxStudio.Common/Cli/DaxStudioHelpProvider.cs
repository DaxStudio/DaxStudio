using System.Collections.Generic;
using System.Reflection;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace DaxStudio.Common.Cli
{
    /// <summary>
    /// Customises Spectre's built-in help renderer so the DAX Studio
    /// version banner is shown above the standard usage / options layout.
    /// All other sections fall through to the default implementation.
    /// </summary>
    internal sealed class DaxStudioHelpProvider : HelpProvider
    {
        public DaxStudioHelpProvider(ICommandAppSettings settings)
            : base(settings)
        {
        }

        public override IEnumerable<IRenderable> GetHeader(ICommandModel model, ICommandInfo command)
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            var build = ver != null ? ver.Revision : 0;
            var versionText = ver != null ? ver.ToString(3) : "0.0.0";

            // Spectre concatenates renderables on the same row unless we
            // insert explicit Text.NewLine breaks. Text.Empty renders nothing
            // (not a blank line), so use Text.NewLine for vertical spacing.
            return new IRenderable[]
            {
                Text.NewLine,
                new Markup($"[bold yellow]DAX Studio[/] v{versionText} (build {build})"), Text.NewLine,
                new Markup("[dim link]https://daxstudio.org[/]"), Text.NewLine,
                Text.NewLine,
            };
        }
    }
}
