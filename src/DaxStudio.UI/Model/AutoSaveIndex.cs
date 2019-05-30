using DaxStudio.UI.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DaxStudio.UI.Model
{
    public class AutoSaveIndex
    {

        public AutoSaveIndex() {
            Files = new List<AutoSaveIndexEntry>();
            LastAutoSaveTime = DateTime.UtcNow;
            ProcessId = Process.GetCurrentProcess().Id;
            IndexId = Guid.NewGuid();
            ShouldRecover = false;
        }

        public DateTime LastAutoSaveTime { get; set; }
        public List<AutoSaveIndexEntry> Files { get; private set; }
        public int ProcessId { get; set; }
        public Guid IndexId { get; set; }

        [JsonIgnore]
        public string IndexFile { get { return $"index-{IndexId}.json"; } }

        public bool ShouldRecover { get; internal set; }

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
    }
}
