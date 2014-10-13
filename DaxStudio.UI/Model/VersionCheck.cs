namespace DaxStudio.UI.Model
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Xml;
    using DaxStudio.UI.Events;
    using Caliburn.Micro;
    public class VersionCheck
    {


        private static string CURRENT_VERSION_URL = "https://daxstudio.svn.codeplex.com/svn/CurrentReleaseVersion.xml";


        public static string DAXSTUDIO_RELEASE_URL = "https://daxstudio.codeplex.com/releases";
        private const int CHECK_EVERY_DAYS = 7;
        private const int CHECK_SECONDS_AFTER_STARTUP = 60;
        private BackgroundWorker worker = new BackgroundWorker();
        private readonly EventAggregator _eventAggregator;
        /// <summary>
        /// The latest version from CodePlex. Use a class field to prevent repeat calls, this acts as a cache.
        /// </summary>
        private string serverVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionCheckPlugin"/> class.
        /// </summary>
        /// <param name="eventAggregator">A reference to the event aggregator so we can publish an event when a new version is found.</param>
        public VersionCheck(EventAggregator eventAggregator)
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
            System.Threading.Thread.Sleep(CHECK_SECONDS_AFTER_STARTUP * 1000); //give BIDS a little time to get started up so we don't impede work people are doing with this version check
            CheckVersion();
        }

        public void CheckVersion()
        {
            try
            {
                if (!VersionIsLatest(LocalVersion, ServerVersion) && ServerVersion != DismissedVersion)
                {
                    _eventAggregator.PublishOnUIThread(new NewVersionEvent(ServerVersion, DAXSTUDIO_RELEASE_URL));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
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


        public string DismissedVersion
        {
            get
            {
                return RegistryHelper.GetDismissedVersion();
            }
            set
            {
                RegistryHelper.SetDismissedVersion(value);
            }
        }

        public static string LocalVersion
        {
            get
            {
                return typeof(VersionCheck).Assembly.GetName().Version.ToString();
            }
        }

        public string ServerVersion
        {
            get
            {
                if (this.serverVersion != null)
                {
                    return this.serverVersion;
                }

                System.Net.WebClient http = new System.Net.WebClient();
                //http.Proxy = System.Net.WebProxy.GetDefaultProxy(); //works but is deprecated
                http.Proxy = System.Net.WebRequest.GetSystemWebProxy(); //inherits the Internet Explorer proxy settings. Should help this version check work behind a proxy server.

                MemoryStream ms = new MemoryStream(http.DownloadData(new Uri(CURRENT_VERSION_URL)));
                XmlReader reader = XmlReader.Create(ms);
                XmlDocument doc = new XmlDocument();
                doc.Load(reader);
                this.serverVersion = doc.DocumentElement.SelectSingleNode("Version").InnerText;
                ms.Close();
                reader.Close();

                return this.serverVersion;
            }
        }

        public static bool VersionIsLatest(string sLocalVersion, string sServerVersion)
        {
            string[] arrLocalVersion = sLocalVersion.Split('.');
            string[] arrServerVersion = sServerVersion.Split('.');

            for (int i = 0; i < Math.Max(arrLocalVersion.Length, arrServerVersion.Length); i++)
            {
                int iLocal = 0;
                if (arrLocalVersion.Length > i) iLocal = int.Parse(arrLocalVersion[i]);
                int iServer = 0;
                if (arrServerVersion.Length > i) iServer = int.Parse(arrServerVersion[i]);
                if (iLocal < iServer)
                {
                    return false;
                }
                else if (iLocal > iServer)
                {
                    return true;
                }
            }

            return true;
        }

        public static void OpenBidsHelperReleasePageInBrowser()
        {
            // Open URL in Browser
            System.Diagnostics.Process.Start(DAXSTUDIO_RELEASE_URL);
        }

    }
}

