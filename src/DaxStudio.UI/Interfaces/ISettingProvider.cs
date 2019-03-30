using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace DaxStudio.UI.Interfaces
{
    public interface ISettingProvider
    {
        ObservableCollection<string> GetServerMRUList();

        void SaveServerMRUList(string currentServer, ObservableCollection<string> servers);

        ObservableCollection<DaxFile> GetFileMRUList();

        void SaveFileMRUList(IEnumerable<object> files);

        T GetValue<T>(string subKey, T defaultValue);

        bool IsFileLoggingEnabled();

        Task SetValueAsync<T>(string subKey, T value);

        void SetLastVersionCheck(DateTime value);

        DateTime GetLastVersionCheck();

        void SetWindowPosition(string value);

        string GetWindowPosition();

        string GetDismissedVersion();

        void SetDismissedVersion(string value);

    }
}

