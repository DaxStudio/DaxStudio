using DaxStudio.Interfaces;
using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        void SetValue<T>(string subKey, T value,  bool isInitializing, object options, [System.Runtime.CompilerServices.CallerMemberName] string propertyName ="");
        void SetValue(string subKey, DateTime value, bool isInitializing, object options, [System.Runtime.CompilerServices.CallerMemberName] string propertyName="");

        bool IsFileLoggingEnabled();
        string LogPath { get; }

        void Initialize(IGlobalOptions options);

        bool IsRunningPortable { get; }
        string SettingsFile { get; }
    }
}

