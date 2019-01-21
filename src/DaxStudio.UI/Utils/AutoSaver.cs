using DaxStudio.Common;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    public static class AutoSaver
    {
        private static Dictionary<int, AutoSaveIndex> _masterAutoSaveIndex;

        static AutoSaver()
        {
            CreateAutoSaveFolder();
            _masterAutoSaveIndex = new Dictionary<int, AutoSaveIndex>();
        }

        private static void CreateAutoSaveFolder()
        {
            Directory.CreateDirectory(AutoSaveFolder);
        }

        static string AutoSaveFolder => Environment.ExpandEnvironmentVariables(Constants.AutoSaveFolder); 
        

        public async static Task Save(DocumentTabViewModel tabs)
        {
            try
            {
                // exit here if no tabs are open
                if (tabs.Items.Count == 0) return;

                var currentProcessId = Process.GetCurrentProcess().Id;

                AutoSaveIndex index;
                _masterAutoSaveIndex.TryGetValue(currentProcessId, out index);
                if (index == null)
                {
                    index = new AutoSaveIndex();
                    _masterAutoSaveIndex.Add(currentProcessId, index);
                }

                foreach (DocumentViewModel tab in tabs.Items)
                {
                    if (tab.IsDirty) index.Add(tab);

                    // don't autosave if the document has not changed since last save
                    // or if IsDirty is false meaning that the file has been manually saved
                    if (tab.IsDirty || tab.LastAutoSaveUtcTime < tab.LastModifiedUtcTime) 
                        await tab.AutoSave();
                    
                }
                SaveIndex(index);
            } 
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "AutoSaver", "Save", ex.Message);
            }
        }



        private static void SaveMasterIndex()
        {
            // TODO - saves current process details to master index
        }


        // this gets called from a timer, so it's already running off the UI thread, so this IO should not be blocking
        private static void SaveIndex(AutoSaveIndex index)
        {
            JsonSerializer serializer = new JsonSerializer();
            using (StreamWriter sw = new StreamWriter(AutoSaveIndexFile(index)))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, index);
            }
        }

        // called on a clean shutdown, removes all autosave files
        internal static void RemoveAll()
        {
            // delete autosaveindex
            File.Delete(AutoSaveMasterIndexFile);
            // delete autosave files
            System.IO.DirectoryInfo di = new DirectoryInfo(AutoSaveFolder);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
        }


        internal static string AutoSaveIndexFile(AutoSaveIndex index)
        {
            return Path.Combine(Environment.ExpandEnvironmentVariables(Constants.AutoSaveFolder),index.IndexFile); 
        }

        internal static string AutoSaveMasterIndexFile
        {
            get
            {
                return Environment.ExpandEnvironmentVariables(Constants.AutoSaveIndexPath);
            }
        }

        internal static Dictionary<int,AutoSaveIndex> LoadAutoSaveMasterIndex()
        {
            JsonSerializer serializer = new JsonSerializer();

            // if the auto save index does not exist return an empty index
            if (!File.Exists(AutoSaveMasterIndexFile)) return _masterAutoSaveIndex;
            try
            {
                using (StreamReader sr = new StreamReader(AutoSaveMasterIndexFile))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    _masterAutoSaveIndex = serializer.Deserialize<Dictionary<int,AutoSaveIndex>>(reader);
                    UpdateMasterIndexForRunningInstances();
                    return _masterAutoSaveIndex;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "AutoSaver", "GetAutoSaveIndex", $"Error loading auto save index: {ex.Message}");
                return _masterAutoSaveIndex;
            }
        }

        private static void UpdateMasterIndexForRunningInstances()
        {
            var currentProcessFileName = Process.GetCurrentProcess().StartInfo.FileName;

            foreach( int procId in _masterAutoSaveIndex.Keys)
            {
                try
                {
                    var process = Process.GetProcessById(procId);
                    // if this process id belongs to another exe the previous 
                    // DAX Studio process must have crashed and needs to be recovered
                    if (process.StartInfo.FileName != currentProcessFileName)
                        _masterAutoSaveIndex[procId].ShouldRecover = true;
                }
                catch (ArgumentException )
                {
                    // if this process id does not exist the previous 
                    // DAX Studio process must have crashed and needs to be recovered
                    _masterAutoSaveIndex[procId].ShouldRecover = true;
                }
                
            }
        }

        internal static void EnsureDirectoryExists(string filePath)
        {
            FileInfo fi = new FileInfo(filePath);
            if (!fi.Directory.Exists)
            {
                System.IO.Directory.CreateDirectory(fi.DirectoryName);
            }
        }

        internal static string GetAutoSaveText(Guid autoSaveId)
        {
            try
            {
                var fileName = Path.Combine(Environment.ExpandEnvironmentVariables(Constants.AutoSaveFolder), $"{autoSaveId}.dax");

                using (TextReader tr = new StreamReader(fileName, true))
                {
                    // put contents in edit window
                    var text = tr.ReadToEnd();
                    return text;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "AutoSaver", "GetAutoSaveText", ex.Message);
                return "-- <<< ERROR READING AUTOSAVE FILE >>> --";
            }
        }
    }
}
