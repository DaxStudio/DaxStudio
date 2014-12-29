namespace DaxStudio.UI.Model
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Xml;
    using DaxStudio.UI.Events;
    using Caliburn.Micro;
    using DaxStudio.Interfaces;
    using System.ComponentModel.Composition;
    using Serilog;

    [Export(typeof(IVersionCheck)), PartCreationPolicy(CreationPolicy.Shared)]
    public class VersionCheck : PropertyChangedBase, IVersionCheck
    {

        private const string CURRENT_VERSION_URL = "https://daxstudio.svn.codeplex.com/svn/DaxStudio/CurrentReleaseVersion.xml";
        private const string DAXSTUDIO_RELEASE_URL = "https://daxstudio.codeplex.com/releases";
        private const int CHECK_EVERY_DAYS = 3;
        private const int CHECK_SECONDS_AFTER_STARTUP = 15;
        private BackgroundWorker worker = new BackgroundWorker();
        private readonly IEventAggregator _eventAggregator;
        /// <summary>
        /// The latest version from CodePlex. Use a class field to prevent repeat calls, this acts as a cache.
        /// </summary>
        private Version serverVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionCheckPlugin"/> class.
        /// </summary>
        /// <param name="eventAggregator">A reference to the event aggregator so we can publish an event when a new version is found.</param>
        [ImportingConstructor]
        public VersionCheck(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;

            if (this.Enabled && LastVersionCheck.AddDays(CHECK_EVERY_DAYS) < DateTime.Today)
            {
                worker.DoWork += new DoWorkEventHandler(worker_DoWork);
                worker.RunWorkerAsync();
            }
        }

        public bool Enabled
        {
            get
            {
                return true;
            }
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            System.Threading.Thread.Sleep(CHECK_SECONDS_AFTER_STARTUP * 1000); //give DaxStudio a little time to get started up so we don't impede work people are doing with this version check
            LastVersionCheck = DateTime.Now;
            CheckVersion();
        }

        public void CheckVersion()
        {
            try
            {
                if (!VersionIsLatest && ServerVersion != DismissedVersion)
                {
                    _eventAggregator.PublishOnUIThread(new NewVersionEvent(ServerVersion, DAXSTUDIO_RELEASE_URL));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
                Log.Error("Class: {0} Method: {1} Exception: {2} Stacktrace: {3}", "VersionCheck", "CheckVersion", ex.Message, ex.StackTrace);
            }
        }


        public DateTime LastVersionCheck
        {
            get
            {
                return RegistryHelper.GetLastVersionCheck();
            }
            set
            {
                RegistryHelper.SetLastVersionCheck(value);
            }
        }


        public Version DismissedVersion
        {
            get
            {
                return Version.Parse(RegistryHelper.GetDismissedVersion());
            }
            set
            {
                RegistryHelper.SetDismissedVersion(value.ToString());
            }
        }

        public Version LocalVersion
        {
            get
            {
                return typeof(VersionCheck).Assembly.GetName().Version;
            }
        }

        public Version ServerVersion
        {
            get
            {
                if (this.serverVersion != null)
                {
                    return this.serverVersion;
                }
                VersionStatus = "Checking for updates...";
                NotifyOfPropertyChange(() => VersionStatus);
         
                System.Net.WebClient http = new System.Net.WebClient();
                //http.Proxy = System.Net.WebProxy.GetDefaultProxy(); //works but is deprecated
                http.Proxy = System.Net.WebRequest.GetSystemWebProxy(); //inherits the Internet Explorer proxy settings. Should help this version check work behind a proxy server.
                MemoryStream ms;
                try { 
                    ms = new MemoryStream(http.DownloadData(new Uri(CURRENT_VERSION_URL)));
                }    
                catch (System.Net.WebException wex)
                {
                    if (wex.Status == System.Net.WebExceptionStatus.ProtocolError)
                    {
                        // assume proxy auth error and re-try with current user credentials
                        http.Proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
                        ms = new MemoryStream(http.DownloadData(new Uri(CURRENT_VERSION_URL)));
                    }
                    else
                    {
                        throw;
                    }
                }
                XmlReader reader = XmlReader.Create(ms);
                XmlDocument doc = new XmlDocument();
                doc.Load(reader);
                this.serverVersion = Version.Parse(doc.DocumentElement.SelectSingleNode("Version").InnerText);
                ms.Close();
                reader.Close();
                
                if (LocalVersion.CompareTo(serverVersion) > 0)
                    { VersionStatus = string.Format("(Ahead of official release - {0} )",serverVersion.ToString());}
                else if (LocalVersion.CompareTo(serverVersion) == 0)
                    { VersionStatus = "(Latest Official Release)"; }
                else
                    { VersionStatus = string.Format("(New Version available - {0})", serverVersion.ToString()); }
            
                NotifyOfPropertyChange(() => VersionStatus);

                return this.serverVersion;
            }
        }

        public bool VersionIsLatest
        {
            get
            {
                return LocalVersion.CompareTo(ServerVersion) >= 0;
            }
        }

        public string VersionStatus { get; set; }

        public static void OpenDaxStudioReleasePageInBrowser()
        {
            // Open URL in Browser
            System.Diagnostics.Process.Start(DAXSTUDIO_RELEASE_URL);
        }
        public void Update()
        {
            if (serverVersion != null) return;
            var ver = this.ServerVersion;
        }

    }
}

