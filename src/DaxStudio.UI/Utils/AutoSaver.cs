using DaxStudio.Common;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    public static class AutoSaver
    {
        private const int INDEX_VERSION = 1;
        private static Dictionary<int, AutoSaveIndex> _masterAutoSaveIndex;

        static AutoSaver()
        {
            CreateAutoSaveFolder();
            _masterAutoSaveIndex = new Dictionary<int, AutoSaveIndex>();
            //LoadAutoSaveMasterIndex();
            GetCurrentAutoSaveIndex();
        }

        private static void CreateAutoSaveFolder()
        {
            Directory.CreateDirectory(AutoSaveFolder);
        }

        static string AutoSaveFolder => Environment.ExpandEnvironmentVariables(Constants.AutoSaveFolder); 
        
        static int CurrentProcessId => Process.GetCurrentProcess().Id;

        private static AutoSaveIndex GetCurrentAutoSaveIndex()
        {
            AutoSaveIndex index;
            _masterAutoSaveIndex.TryGetValue(CurrentProcessId, out index);
            if (index == null)
            {
                index = AutoSaveIndex.Create();
                _masterAutoSaveIndex.Add(CurrentProcessId, index);
            }
            return index;
        }

        public async static Task Save(DocumentTabViewModel tabs)
        {
            try
            {
                // exit here if no tabs are open
                if (tabs.Items.Count == 0) return;


                var index = GetCurrentAutoSaveIndex();

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



        //private static void SaveMasterIndex()
        //{
        //    // TODO - saves current process details to master index
        //    JsonSerializer serializer = new JsonSerializer();
        //    using (StreamWriter sw = new StreamWriter(AutoSaveMasterIndexFile))
        //    using (JsonWriter writer = new JsonTextWriter(sw))
        //    {
        //        serializer.Serialize(writer, _masterAutoSaveIndex);
        //    }
        //}


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
            File.Delete(AutoSaveIndexFile(GetCurrentAutoSaveIndex()));

            // delete autosave files
            // TODO - should I only delete the files for this instance of DAX Studio??
            //        at the moment this removes all auto save files from all instances
            //        so if one instance crashes and another is still open closing the open one
            //        will wipe out any files from the crashed instance... 
            System.IO.DirectoryInfo di = new DirectoryInfo(AutoSaveFolder);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }

            // remove entry from master auto save index
            _masterAutoSaveIndex.Remove(CurrentProcessId);
        }

        // theis method deletes any indexes/files that have been recovered
        public static void CleanUpRecoveredFiles()
        {
            try
            {
                Log.Debug("{class} {method} {message}", "AutoSaver", "CleanUpRecoveredFiles", "Removing autosave files that are no longer needed");

                var filesToDelete = _masterAutoSaveIndex.Values.Where(i => i.ShouldRecover).SelectMany(entry => entry.Files);
                var indexesToDelete = _masterAutoSaveIndex.Values.Where(i => i.ShouldRecover);

                foreach (var f in filesToDelete)
                {
                    File.Delete(Path.Combine(AutoSaveFolder, $"{f.AutoSaveId}.dax"));
                }

                for (int i = indexesToDelete.Count() -1; i >= 0; i-- )
                {
                    var idx = indexesToDelete.ElementAt(i);
                    File.Delete(Path.Combine(AutoSaveFolder, $"index-{idx.IndexId}.json"));
                    _masterAutoSaveIndex.Remove(idx.ProcessId);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex,"{class} {method} {message}", "AutoSaver", "CleanUpRecoveredFiles", "Error Removing autosave files that are no longer needed");
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
            try
            {
                // get all index-*.json files
                var indexFiles = Directory.GetFiles(AutoSaveFolder, "*.json");
                foreach (var f in indexFiles)
                {
                    var idx = LoadAutoSaveIndex(f);
                    _masterAutoSaveIndex.Add(idx.ProcessId, idx);
                }
                UpdateMasterIndexForRunningInstances();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "AutoSaver", "GetAutoSaveIndex", $"Error loading auto save index: {ex.Message}");
                return _masterAutoSaveIndex;
            }
            return _masterAutoSaveIndex;
        }

        private static void UpdateMasterIndexForRunningInstances()
        {
            //var currentProcessFileName = Process.GetCurrentProcess().StartInfo.FileName;
            var currentProcessName = Process.GetCurrentProcess().ProcessName;

            foreach (int procId in _masterAutoSaveIndex.Keys)
            {
                var indexToRecover = _masterAutoSaveIndex[procId];
                try
                {
                    var process = Process.GetProcessById(procId);
                    // if this process id belongs to another exe the previous 
                    // DAX Studio process must have crashed and needs to be recovered
                    if (process.ProcessName != currentProcessName)
                    {
                        _masterAutoSaveIndex[procId].ShouldRecover = true;
                    }
                }
                catch (ArgumentException)
                {
                    // if this process id does not exist the previous 
                    // DAX Studio process must have crashed and needs to be recovered
                    _masterAutoSaveIndex[procId].ShouldRecover = true;
                }

            }
        }

        private static AutoSaveIndex LoadAutoSaveIndex(string indexFile)
        {
            JsonSerializer serializer = new JsonSerializer();

            AutoSaveIndex idx;// = AutoSaveIndex.Create();
            
            // if the auto save index does not exist return an empty index            
            try
            {
                using (StreamReader sr = new StreamReader(indexFile))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    idx = serializer.Deserialize<AutoSaveIndex>(reader);
                    return idx;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "AutoSaver", "LoadAutoSaveIndex", $"Error loading auto save index '{indexFile}' : {ex.Message}");
                return null;
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
