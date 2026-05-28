using System.Windows;

namespace DaxStudio.Common
{
    public static class ApplicationArgsExtensions
    {
        public static CmdLineArgs Args(this Application app)
        {
            return new CmdLineArgs(app.Properties);
        }
    }
}
