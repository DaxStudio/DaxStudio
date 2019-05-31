using DaxStudio.UI.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DaxStudio.UI.Model
{
    public class AutoSaveIndex
    {
        public const int CurrentVersion = 1;

        public AutoSaveIndex() {
            Version = 0;
            Files = new List<AutoSaveIndexEntry>();
            LastAutoSaveTime = DateTime.UtcNow;
            ProcessId = Process.GetCurrentProcess().Id;
            IndexId = Guid.NewGuid();
            ShouldRecover = false;
        }

        public AutoSaveIndex(int version):this()
        {
            Version = version;
        }

        public DateTime LastAutoSaveTime { get; set; }
        public List<AutoSaveIndexEntry> Files { get; private set; }
        public int ProcessId { get; set; }
        public Guid IndexId { get; set; }

        public int Version { get; set; }

        [JsonIgnore]
        public string IndexFile { get { return $"index-{IndexId}.json"; } }

        public bool ShouldRecover { get; internal set; }

        [JsonIgnore]
        public bool IsCurrentVersion => this.Version == CurrentVersion;

        public void Add(DocumentViewModel document)
        {
            var existingEntry = Files.Find(f => f.AutoSaveId == document.AutoSaveId);
            if (existingEntry != null) return; 

            Files.Add(new AutoSaveIndexEntry()
            {
                AutoSaveId = document.AutoSaveId,
                IsDiskFileName = document.IsDiskFileName,
                DisplayName = document.DisplayName,
                OriginalFileName = document.IsDiskFileName ? document.FileName : "",
            });
        }

        internal static AutoSaveIndex Create()
        {
            return new AutoSaveIndex(CurrentVersion);
        }
    }
}
