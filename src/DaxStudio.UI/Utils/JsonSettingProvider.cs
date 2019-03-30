using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class JsonSettingProvider : ISettingProvider
    {
        private static string settingsFile;
        private static string appPath;

        static JsonSettingProvider()
        {
            appPath = AppDomain.CurrentDomain.BaseDirectory;
            settingsFile = Path.Combine(appPath, "settings.json");
        }

        #region Static Members
        public static bool SettingsFileExists()
        {
            return File.Exists(settingsFile);
        }

        public static string LogPath => Path.Combine(appPath, "logs");
        #endregion

        public string GetDismissedVersion()
        {
            throw new NotImplementedException();
        }

        public ObservableCollection<DaxFile> GetFileMRUList()
        {
            throw new NotImplementedException();
        }

        public DateTime GetLastVersionCheck()
        {
            throw new NotImplementedException();
        }

        public ObservableCollection<string> GetServerMRUList()
        {
            throw new NotImplementedException();
        }

        public T GetValue<T>(string subKey, T defaultValue)
        {
            throw new NotImplementedException();
        }

        public string GetWindowPosition()
        {
            throw new NotImplementedException();
        }

        public bool IsFileLoggingEnabled()
        {
            throw new NotImplementedException();
        }

        public void SaveFileMRUList(IEnumerable<object> files)
        {
            throw new NotImplementedException();
        }

        public void SaveServerMRUList(string currentServer, ObservableCollection<string> servers)
        {
            throw new NotImplementedException();
        }

        public void SetDismissedVersion(string value)
        {
            throw new NotImplementedException();
        }

        public void SetLastVersionCheck(DateTime value)
        {
            throw new NotImplementedException();
        }

        public Task SetValueAsync<T>(string subKey, T value)
        {
            throw new NotImplementedException();
        }

        public void SetWindowPosition(string value)
        {
            throw new NotImplementedException();
        }
    }
}
