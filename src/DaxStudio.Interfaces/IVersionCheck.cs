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
        void Update();
        Uri DownloadUrl { get; }

        event PropertyChangedEventHandler PropertyChanged;

        event EventHandler UpdateCompleteCallback;
        event EventHandler UpdateStartingCallback;
    }
}
