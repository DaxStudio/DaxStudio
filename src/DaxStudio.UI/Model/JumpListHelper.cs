using DaxStudio.Common;
using Serilog;
using System.Windows;
using System.Windows.Shell;

namespace DaxStudio.UI.Model
{
    public static class JumpListHelper
    {
        public static void ConfigureJumpList(Application app)
        {
            JumpList jumpList = JumpList.GetJumpList(app);
            if (jumpList == null)
            {
                jumpList = CreateJumpList();
            }
            jumpList.Apply();
            Log.Debug(Constants.LogMessageTemplate, nameof(JumpListHelper), nameof(ConfigureJumpList), $"Jumplist initialized with {jumpList.JumpItems.Count} item(s)");
        }

        public static void AddToRecentFilesList(string fileName)
        {
            var jumpList = JumpList.GetJumpList(Application.Current);
            if (jumpList == null)
            {
                jumpList = CreateJumpList();
            }

            // This adds a JumpTask to the recent files list using the same format that 
            // Windows Explorer uses for recent files when double clicking a file
            var workingDir = System.IO.Path.GetDirectoryName(fileName);
            var fileNameOnly = System.IO.Path.GetFileName(fileName);
            JumpTask jt= new JumpTask() { 
                ApplicationPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,
                Arguments = $"-file \"{fileName}\"",
                Title = fileNameOnly,
                Description = $"{System.IO.Path.GetFileNameWithoutExtension(fileNameOnly)} ({workingDir})",
                WorkingDirectory = workingDir
            };
            JumpList.AddToRecentCategory(jt);
            
            Log.Debug(Constants.LogMessageTemplate, nameof(JumpListHelper), nameof(ConfigureJumpList), $"Added '{fileName}' to Jumplist with {jumpList?.JumpItems?.Count??0} items");
        }

        private static JumpList CreateJumpList()
        {
            Log.Debug(Constants.LogMessageTemplate, nameof(JumpListHelper), nameof(CreateJumpList), $"Creating new Jumplist");
            JumpList jumpList = new JumpList();
            jumpList.ShowRecentCategory = true;
            jumpList.Apply();
            JumpList.SetJumpList(Application.Current, jumpList);
            return jumpList;
        }
    }
}
