using DaxStudio.Common;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    public static class AutoSaver
    {
        public async static Task Save(DocumentTabViewModel tabs)
        {
            try
            {
                // exit here if no tabs are open
                if (tabs.Items.Count == 0) return;

                var index = new AutoSaveIndex();
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

        // this gets called from a timer, so it's already running off the UI thread, so this IO should not be blocking
        private static void SaveIndex(AutoSaveIndex index)
        {
            JsonSerializer serializer = new JsonSerializer();
            using (StreamWriter sw = new StreamWriter(AutoSaveIndexFile))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, index);
            }
        }

        // called on a clean shutdown, removes all autosave files
        internal static void RemoveAll()
        {
            // delete autosaveindex
            File.Delete(AutoSaveIndexFile);
            // delete autosave files
            System.IO.DirectoryInfo di = new DirectoryInfo(Environment.ExpandEnvironmentVariables(Constants.AutoSaveFolder));
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
        }

        internal static string AutoSaveIndexFile
        {
            get { return Environment.ExpandEnvironmentVariables(Constants.AutoSaveIndexPath); }
        }

        internal static AutoSaveIndex GetAutoSaveIndex()
        {
            JsonSerializer serializer = new JsonSerializer();

            // if the auto save index does not exist return an empty index
            if (!File.Exists(AutoSaveIndexFile)) return new AutoSaveIndex();
            try
            {
                using (StreamReader sr = new StreamReader(AutoSaveIndexFile))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    var index = serializer.Deserialize<AutoSaveIndex>(reader);
                    return index ?? new AutoSaveIndex();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "AutoSaver", "GetAutoSaveIndex", $"Error loading auto save index: {ex.Message}");
                return new AutoSaveIndex();
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
