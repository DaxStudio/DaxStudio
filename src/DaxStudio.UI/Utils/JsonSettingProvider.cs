using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    public class JsonSettingProvider : ISettingProvider
    {
        private static string settingsFile;

        static JsonSettingProvider()
        {
            string folderPath = AppDomain.CurrentDomain.BaseDirectory;
            settingsFile = Path.Combine(folderPath, "settings.json");
        }

        public static bool SettingsFileExists()
        {
            return File.Exists(settingsFile);
        }

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
