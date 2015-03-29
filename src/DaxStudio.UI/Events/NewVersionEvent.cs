using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class NewVersionEvent
    {
        public NewVersionEvent(Version newVersion, string downloadUrl)
        {
            NewVersion = newVersion;
            DownloadUrl = downloadUrl;
        }

        public Version NewVersion { get; private set; }

        public string DownloadUrl { get; private set; }
    }
}
