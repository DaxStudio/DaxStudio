using System;

namespace DaxStudio.UI.Events
{
    public class NewVersionEvent
    {
        public NewVersionEvent(Version newVersion, Uri downloadUrl)
        {
            NewVersion = newVersion;
            DownloadUrl = downloadUrl;
        }

        public Version NewVersion { get; private set; }

        public Uri DownloadUrl { get; private set; }
    }
}
