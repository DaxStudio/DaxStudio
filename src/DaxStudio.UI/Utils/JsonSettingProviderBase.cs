using DaxStudio.Interfaces;
using DaxStudio.UI.Extensions;
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
    public abstract class JsonSettingProviderBase : ISettingProvider
    {
        public abstract string SettingsPath { get; }

        private readonly string settingsFile;
        private readonly string recentFilesFile;
        private readonly string recentServersFile;
        
        private IGlobalOptions _options;
        private IDictionary<string, object> _optionsDict;

        
        //[Import]
        public IGlobalOptions Options {
            get => _options; 
            set {
                _options = value;
                _optionsDict = _options.AsDictionary();
            }
        }

        public JsonSettingProviderBase()
        {
            // todo - if running portable use local path, otherwise use AppData
            
            settingsFile = Path.Combine(SettingsPath, "settings.json");
            recentServersFile = Path.Combine(SettingsPath, "recentServers.json");
            recentFilesFile = Path.Combine(SettingsPath, "recentFiles.json");
        }

        #region Static Members
        public bool SettingsFileExists()
        {
            return File.Exists(settingsFile);
        }

        public string LogPath => Path.Combine(SettingsPath, "logs");
        #endregion

        //public string GetDismissedVersion()
        //{
        //    throw new NotImplementedException();
        //}

        public ObservableCollection<DaxFile> GetFileMRUList()
        {
            throw new NotImplementedException();
        }

        //public DateTime GetLastVersionCheck()
        //{
        //    throw new NotImplementedException();
        //}

        public ObservableCollection<string> GetServerMRUList()
        {
            throw new NotImplementedException();
        }

        public T GetValue<T>(string subKey, T defaultValue)
        {
            return ((T)_optionsDict[subKey]);
        }
        
        public Task SetValueAsync<T>(string subKey, T value, bool isInitializing)
        {
            
            return Task.Run(() => {
                if (isInitializing) return;
                _optionsDict[subKey] = value;
                // TODO - write json file

            });
            
        }

        //public string GetWindowPosition()
        //{
        //    throw new NotImplementedException();
        //}

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

        public void Initialize(IGlobalOptions options)
        {
            // TODO - load settings from settings.json
            Options = options;
            throw new NotImplementedException();
        }

        //public void SetDismissedVersion(string value)
        //{
        //    throw new NotImplementedException();
        //}

        //public void SetLastVersionCheck(DateTime value)
        //{
        //    throw new NotImplementedException();
        //}


        //public void SetWindowPosition(string value)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
