using System;
using System.IO;

namespace DaxStudio.Common
{
    public static class ApplicationPaths
    {
        static ApplicationPaths()
        {
            //To get the location the assembly normally resides on disk or the install directory
            string path = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
            var directory = Path.GetDirectoryName(path);
            PortableFile = Path.Combine(directory, @"bin\.portable");
            IsInPortableMode = File.Exists(PortableFile);

            BasePath = IsInPortableMode 
                           ? directory
                           : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DaxStudio");

            BaseLocalPath = IsInPortableMode
               ? directory
               : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaxStudio");

            LogPath = Path.Combine(BaseLocalPath, "Log");
            QueryHistoryPath = Path.Combine(BasePath, "QueryHistory");
            AutoSavePath = Path.Combine(BaseLocalPath, "AutoSaveFiles");
        }

        public static string LogPath {get;}
        public static string QueryHistoryPath { get; }
        public static string PortableFile { get; }
        public static string AutoSavePath { get; }
        private static bool IsInPortableMode { get; }
        public static string BasePath { get; }
        internal static string BaseLocalPath { get; }
        public static string AvalonDockLayoutFile => Path.Combine(BaseLocalPath, "WindowLayouts", "Custom.xml");

    }
}
