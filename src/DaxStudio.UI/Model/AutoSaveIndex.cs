using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;

namespace DaxStudio.UI.Model
{
    public class AutoSaveIndex
    {

        public AutoSaveIndex() {
            Files = new List<AutoSaveIndexEntry>();
            LastAutoSaveTime = DateTime.UtcNow;
        }
        public DateTime LastAutoSaveTime { get; set; }
        public List<AutoSaveIndexEntry> Files { get; private set; }

        public void Add(DocumentViewModel document)
        {
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
