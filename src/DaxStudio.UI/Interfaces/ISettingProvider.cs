using DaxStudio.Interfaces;
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

        ObservableCollection<IDaxFile> GetFileMRUList();
        void SaveFileMRUList(IDaxFile file, ObservableCollection<IDaxFile> files);

        T GetValue<T>(string subKey, T defaultValue);
        Task SetValueAsync<T>(string subKey, T value, bool isInitializing);

        bool IsFileLoggingEnabled();
        string LogPath { get; }

        void Initialize(IGlobalOptions options);

        bool IsRunningPortable { get; }
    }
}

