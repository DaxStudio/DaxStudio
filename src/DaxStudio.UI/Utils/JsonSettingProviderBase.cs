using DaxStudio.Interfaces;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using Newtonsoft.Json;
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
            ReadSettings();
        }

        private void ReadSettings()
        {
            // deserialize JSON directly from a file
            using (StreamReader file = File.OpenText(settingsFile))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
                Options = (IGlobalOptions)serializer.Deserialize(file, typeof(GlobalOptions));
            }

            //var text = File.ReadAllText(settingsFile);
            //Options = JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
        }

        #region Static Members
        public bool SettingsFileExists()
        {
            return File.Exists(settingsFile);
        }

        public string LogPath => Path.Combine(SettingsPath, "logs");
        #endregion
        

        public ObservableCollection<DaxFile> GetFileMRUList()
        {
            // TODO - get real list
            return new ObservableCollection<DaxFile>();
        }
        

        public ObservableCollection<string> GetServerMRUList()
        {
            // TODO - get real list
            return new ObservableCollection<string>();
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
                // write json file
                using (StreamWriter file = File.CreateText(settingsFile))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Formatting = Formatting.Indented;
                    serializer.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
                    serializer.Serialize(file, Options);
                }
            });
            
        }
        

        public bool IsFileLoggingEnabled()
        {
            // TODO
            return false;
        }

        public void SaveFileMRUList(IEnumerable<object> files)
        {
            // TODO
            throw new NotImplementedException();
        }

        public void SaveServerMRUList(string currentServer, ObservableCollection<string> servers)
        {
            // TODO
            throw new NotImplementedException();
        }

        public void Initialize(IGlobalOptions options)
        {
            // TODO - load settings from settings.json
            var json = File.ReadAllText(settingsFile);
            var settings = new JsonSerializerSettings();
            settings.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
            JsonConvert.PopulateObject(json, options, settings);
            Options = options;
            //throw new NotImplementedException();
        }
        
    }
}
