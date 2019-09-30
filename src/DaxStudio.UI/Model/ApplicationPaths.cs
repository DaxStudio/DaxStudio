using System;
using System.IO;

namespace DaxStudio.UI.Model
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
                           : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            
            LogPath = Path.Combine(BasePath, "Log");
            QueryHistoryPath = Path.Combine(BasePath, "QueryHistory");
            
        }

        public static string LogPath {get;}
        public static string QueryHistoryPath { get; }
        public static string PortableFile { get; }
        private static bool IsInPortableMode { get; }
        public static string BasePath { get; }


    }
}
