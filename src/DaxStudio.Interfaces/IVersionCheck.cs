using System;
using System.ComponentModel;

namespace DaxStudio.Interfaces
{
    public interface IVersionCheck
    {
        void CheckVersion();
        Version DismissedVersion { get; set; }
        DateTime LastVersionCheck { get; set; }
        Version ServerVersion { get; }

        Version LocalVersion { get; }
        bool VersionIsLatest { get;  }
        bool IsServerVersionPreview { get; }
        void Update();
        void ForceRecheck();
        Uri DownloadUrl { get; }

        event PropertyChangedEventHandler PropertyChanged;

        event EventHandler UpdateCompleteCallback;
        event EventHandler UpdateStartingCallback;
    }
}
