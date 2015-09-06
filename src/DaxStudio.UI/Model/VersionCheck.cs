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
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using DaxStudio.UI.Utils;
    using System.Net;

    [Export(typeof(IVersionCheck)), PartCreationPolicy(CreationPolicy.Shared)]
    public class VersionCheck : PropertyChangedBase, IVersionCheck
    {

        private const int CHECK_EVERY_DAYS = 3;
        private const int CHECK_SECONDS_AFTER_STARTUP = 15;
        
        private BackgroundWorker worker = new BackgroundWorker();
        private readonly IEventAggregator _eventAggregator;
        /// <summary>
        /// The latest version from CodePlex. Use a class field to prevent repeat calls, this acts as a cache.
        /// </summary>
        private Version _serverVersion;

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
                    _eventAggregator.PublishOnUIThread(new NewVersionEvent(ServerVersion, DownloadUrl));
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

        public int LocalBuild
        {
            get
            {
                return typeof(VersionCheck).Assembly.GetName().Version.Build;
            }
        }

        public Version ServerVersion
        {
            get
            {
                if (this._serverVersion != null)
                {
                    return this._serverVersion;
                }

                VersionStatus = "Checking for updates...";
                NotifyOfPropertyChange(() => VersionStatus);
                try
                {
                    PopulateServerVersionFromGithub();
                }
                catch (Exception ex)
                {
                    Log.Error("{class} {method} {error}", "VersionCheck", "ServerVersion.get", ex.Message);
                    _eventAggregator.PublishOnUIThread(new ErrorEventArgs(ex));
                }
                if (_serverVersion == null)
                { VersionStatus = "(Unable to get version information)"; }
                else if (LocalVersion.CompareTo(_serverVersion) > 0)
                { VersionStatus = string.Format("(Ahead of official release - {0} )", _serverVersion.ToString(3)); }
                else if (LocalVersion.CompareTo(_serverVersion) == 0)
                { VersionStatus = "(Latest Official Release)"; }
                else
                { VersionStatus = string.Format("(New Version available - {0})", _serverVersion.ToString(3)); }
            
                NotifyOfPropertyChange(() => VersionStatus);

                return this._serverVersion;
            }
        }

        //private void PopulateServerVersionFromCodeplex()
        //{
        //    System.Net.WebClient http = new System.Net.WebClient();
        //    //http.Proxy = System.Net.WebProxy.GetDefaultProxy(); //works but is deprecated
        //    http.Proxy = System.Net.WebRequest.GetSystemWebProxy(); //inherits the Internet Explorer proxy settings. Should help this version check work behind a proxy server.
        //    MemoryStream ms;
        //    try
        //    {
        //        ms = new MemoryStream(http.DownloadData(new Uri(CURRENT_CODEPLEX_VERSION_URL)));
        //    }
        //    catch (System.Net.WebException wex)
        //    {
        //        if (wex.Status == System.Net.WebExceptionStatus.ProtocolError)
        //        {
        //            // assume proxy auth error and re-try with current user credentials
        //            http.Proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
        //            ms = new MemoryStream(http.DownloadData(new Uri(CURRENT_CODEPLEX_VERSION_URL)));
        //        }
        //        else
        //        {
        //            throw;
        //        }
        //    }
        //    XmlReader reader = XmlReader.Create(ms);
        //    XmlDocument doc = new XmlDocument();
        //    doc.Load(reader);
        //    this._serverVersion = Version.Parse(doc.DocumentElement.SelectSingleNode("Version").InnerText);
        //    ms.Close();
        //    reader.Close();

        //}


        private void PopulateServerVersionFromGithub()
        {
            var wrf = IoC.Get<WebRequestFactory>();
            using (System.Net.WebClient http = wrf.CreateWebClient())
            {
                
                string json = "";
                //await Task.Run(() => {
                    try
                    {
                         json = http.DownloadString(new Uri(WebRequestFactory.CurrentGithubVersionUrl)); 
                    
                    }
                    catch (System.Net.WebException wex)
                    {
                        if (wex.Status == System.Net.WebExceptionStatus.ProtocolError &&  ((HttpWebResponse)wex.Response).StatusCode == HttpStatusCode.ProxyAuthenticationRequired )
                        {
                            // assume proxy auth error and re-try with current user credentials
                            http.Proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
                            json = http.DownloadString(new Uri(WebRequestFactory.CurrentGithubVersionUrl));
                        }
                        else
                        {
                            throw;
                        }
                    }
                //});

                JObject jobj = JObject.Parse(json);

                this._serverVersion = Version.Parse((string)jobj["Version"]);
                this.DownloadUrl = (string)jobj["DownloadUrl"];
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

        public void OpenDaxStudioReleasePageInBrowser()
        {
            // Open URL in Browser
            System.Diagnostics.Process.Start(DownloadUrl);
        }
        public void Update()
        {
            if (_serverVersion != null) return;
            var ver = this.ServerVersion;
        }

        private string _downloadUrl = "https://daxstudio.codeplex.com/releases"; 
        public string DownloadUrl { 
            get { return _downloadUrl; } 
            set { if (value == _downloadUrl) return; 
                _downloadUrl = value; 
                NotifyOfPropertyChange(() => DownloadUrl); 
            } 
        }
    }
}

