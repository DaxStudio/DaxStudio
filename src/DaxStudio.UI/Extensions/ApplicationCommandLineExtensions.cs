using System.Windows;

namespace DaxStudio.Common.Extensions
{
    public static class ApplicationCommandLineExtensions
    {
        public static void ReadCommandLineArgs(this Application app, string[] args)
        {
            app.Args().Clear();
            app.Args().Parse(args);
        }
    }
}
