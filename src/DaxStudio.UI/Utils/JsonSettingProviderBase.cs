using DaxStudio.Common;
using DaxStudio.Interfaces;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.JsonConverters;
using DaxStudio.UI.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    public abstract class JsonSettingProviderBase : ISettingProvider
    {
        public abstract string SettingsPath { get; }

        private readonly string settingsFile;

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
            //ReadSettings();
        }

        //private void ReadSettings()
        //{
        //    // deserialize JSON directly from a file
        //    using (StreamReader file = File.OpenText(settingsFile))
        //    {
        //        JsonSerializer serializer = new JsonSerializer();
        //        serializer.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
        //        serializer.NullValueHandling = NullValueHandling.Ignore;
        //        Options = (IGlobalOptions)serializer.Deserialize(file, typeof(GlobalOptions) );
        //    }

        //}

        #region Static Members
        public bool SettingsFileExists()
        {
            return File.Exists(settingsFile);
        }

        public virtual bool IsRunningPortable => false;

        public string LogPath => Path.Combine(SettingsPath, "logs");
        #endregion
        

        public ObservableCollection<IDaxFile> GetFileMRUList()
        {
            // TODO - get real list
            //return new ObservableCollection<DaxFile>();
            return Options.RecentFiles;
        }
        

        public ObservableCollection<string> GetServerMRUList()
        {
            // TODO - get real list
            //return new ObservableCollection<string>();
            return Options.RecentServers;
        }

        public T GetValue<T>(string subKey, T defaultValue)
        {
            return ((T)_optionsDict[subKey]);
        }
        
        public Task SetValueAsync<T>(string subKey, T value, bool isInitializing)
        {
            
            return Task.Run(() =>
            {
                if (isInitializing) return;
                _optionsDict[subKey] = value;
                // write json file
                SaveSettingsFile();
            });
            
        }

        private void SaveSettingsFile()
        {
            using (StreamWriter file = File.CreateText(settingsFile))
            {
                JsonSerializer serializer = new JsonSerializer();
                //serializer.Converters.Add(new VersionConverter());
                serializer.Formatting = Formatting.Indented;
                serializer.NullValueHandling = NullValueHandling.Ignore;
                serializer.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
                serializer.Serialize(file, Options);
            }
        }

        public bool IsFileLoggingEnabled()
        {
            // TODO
            return false;
        }

        public void SaveFileMRUList(IDaxFile file, ObservableCollection<IDaxFile> files)
        {
            var existingItem = Options.RecentFiles.FirstOrDefault(f => f.FullPath.Equals(file.FullPath, StringComparison.CurrentCultureIgnoreCase));
            // file does not exist in list so add it as the first item
            if (existingItem == null)
            {
                Options.RecentFiles.Insert(0, file);
                while (Options.RecentFiles.Count() > Constants.MAX_RECENT_FILES)
                {
                    Options.RecentFiles.RemoveAt(Options.RecentFiles.Count() - 1);
                }
                SaveSettingsFile();
                return;
            }

            var exisingIndex = Options.RecentFiles.IndexOf(existingItem);
            // file is already first in the list so do nothing
            if (exisingIndex == 0) return;

            // otherwise move the file to first in the list
            Options.RecentFiles.Move(exisingIndex, 0);

            SaveSettingsFile();
        }

        public void SaveServerMRUList(string currentServer, ObservableCollection<string> servers)
        {
            var existingIdx = Options.RecentServers.IndexOf(currentServer);

            // server is already first in the list
            if (existingIdx == 0) return; // do nothing
                    
            if (existingIdx > 0)
            {
                // server exists, make it first in the list
                Options.RecentServers.Move(existingIdx, 0);
            }
            else
            { 
                // server does not exist in list, so insert it as the first item
                Options.RecentServers.Insert(0, currentServer);
                while (Options.RecentServers.Count() > Constants.MAX_MRU_SIZE)
                {
                    Options.RecentServers.RemoveAt(Options.RecentServers.Count() - 1);
                }
            }

            SaveSettingsFile();
        }

        public void Initialize(IGlobalOptions options)
        {
            // load settings from settings.json
            var json = "{}";
            if (SettingsFileExists()) { json = File.ReadAllText(settingsFile); }
            var settings = new JsonSerializerSettings();
            settings.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
            settings.NullValueHandling = NullValueHandling.Ignore;
            settings.Converters.Add(new DaxFileConverter());
            JsonConvert.PopulateObject(json, options, settings);
            Options = options;
            Options.IsRunningPortable = this.IsRunningPortable;
        }
        
    }
}
