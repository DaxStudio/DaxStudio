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
                Log.Debug(Constants.LogMessageTemplate, nameof(JumpListHelper), nameof(ConfigureJumpList), $"Creating new Jumplist");
                jumpList = new JumpList();
                jumpList.ShowRecentCategory = true;
                jumpList.Apply();
                JumpList.SetJumpList(app, jumpList);
            }
            jumpList.Apply();
            Log.Debug(Constants.LogMessageTemplate, nameof(JumpListHelper), nameof(ConfigureJumpList), $"Jumplist initialized with {jumpList.JumpItems.Count} item(s)");
        }

        public static void AddToRecentFilesList(string fileName)
        {
            var jumpList = JumpList.GetJumpList(Application.Current);
            if (jumpList == null)
            {
                Log.Debug(Constants.LogMessageTemplate, nameof(JumpListHelper), nameof(AddToRecentFilesList), $"Creating new Jumplist");
                jumpList = new JumpList();
                jumpList.ShowRecentCategory = true;
                jumpList.Apply();
                JumpList.SetJumpList(Application.Current, jumpList);
            }
            JumpList.AddToRecentCategory(fileName);
            //JumpList.AddToRecentCategory(new JumpPath() { CustomCategory = "DAX Studio", Path = fileName });
            //jumpList.JumpItems.Add(new JumpPath() { Path = fileName });
            Log.Debug(Constants.LogMessageTemplate, nameof(JumpListHelper), nameof(ConfigureJumpList), $"Added '{fileName}' to Jumplist");
        }
    }
}
