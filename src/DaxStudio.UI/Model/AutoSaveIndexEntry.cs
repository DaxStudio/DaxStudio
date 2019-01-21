using DaxStudio.Interfaces;
using Newtonsoft.Json;
using System;
using Caliburn.Micro;
using DaxStudio.UI.Enums;
using System.IO;

namespace DaxStudio.UI.Model
{
    [JsonObject]
    public class AutoSaveIndexEntry : PropertyChangedBase, IOpenable
    {
        // Default ShouldOpen to true
        public AutoSaveIndexEntry() { ShouldOpen = true; }
        public bool IsDiskFileName { get; set; }
        public Guid AutoSaveId { get; set; }

        public string OriginalFileName { get; set; }
        public string DisplayName { get; set; }

        [JsonIgnore]
        public string Folder { get { return IsDiskFileName?Path.GetDirectoryName(OriginalFileName): "<Unsaved File>"; } }

        [JsonIgnore]
        public string ExtensionLabel
        {
            get
            {
                var ext = Path.GetExtension(DisplayName).TrimStart('.').TrimEnd('*').ToUpper();
                return ext == "DAX" ? "" : ext;
            }
        }

        [JsonIgnore]
        public FileIcons Icon { get { return !IsDiskFileName || Path.GetExtension(OriginalFileName).ToLower() == ".dax" ? FileIcons.Dax : FileIcons.Other; } }


        private bool _shouldOpen = true;
        [JsonIgnore]
        public bool ShouldOpen
        {
            get { return _shouldOpen; }
            set
            {
                _shouldOpen = value;
                NotifyOfPropertyChange(() => ShouldOpen);
            }
        }
    }
}
