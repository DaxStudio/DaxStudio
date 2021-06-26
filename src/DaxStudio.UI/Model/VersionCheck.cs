namespace DaxStudio.UI.Model
{
    using Caliburn.Micro;
    using DaxStudio.Common;
    using DaxStudio.Interfaces;
    using DaxStudio.UI.Events;
    using DaxStudio.UI.Utils;
    using Extensions;
    using Newtonsoft.Json.Linq;
    using Serilog;
    using System;
    using System.ComponentModel;
    using System.ComponentModel.Composition;
    using System.IO;
    using System.Net;

    [Export(typeof(IVersionCheck)), PartCreationPolicy(CreationPolicy.Shared)]
    public class VersionCheck : PropertyChangedBase, IVersionCheck
    {

        //private const int CHECK_EVERY_DAYS = 3;
        private const int CHECK_SECONDS_AFTER_STARTUP = 15;
        private const int CHECK_EVERY_HOURS = 24;
        
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private readonly IEventAggregator _eventAggregator;
        private WebRequestFactory _webRequestFactory;
        private Uri _downloadUrl = new Uri( Constants.DownloadUrl);
        private readonly IGlobalOptions _globalOptions;

        private Version _productionVersion;
        private Uri _productionDownloadUrl;
        private string _serverVersionType;
        private bool _isCheckRunning;
        private bool _isAutomaticCheck;
        /// <summary>
        /// Initializes a new instance of the <see cref="VersionCheck"/> class.
        /// </summary>
        /// <param name="eventAggregator">A reference to the event aggregator so we can publish an event when a new version is found.</param>
        [ImportingConstructor]
        public VersionCheck(IEventAggregator eventAggregator,  IGlobalOptions globalOptions)
        {
            _eventAggregator = eventAggregator;
            
            _globalOptions = globalOptions;

            if (_globalOptions.BlockVersionChecks)
            {
                UpdateCompleteCallback?.Invoke(this,null);
                return;
            }

            worker.DoWork += new DoWorkEventHandler(BackgroundGetGitHubVersion);
            if (Enabled && LastVersionCheck.AddHours(CHECK_EVERY_HOURS) < DateTime.UtcNow)
            {
                _isAutomaticCheck = true;
                worker.RunWorkerAsync();
            }
        }

        public bool Enabled =>  true;
            
        private void BackgroundGetGitHubVersion(object sender, DoWorkEventArgs e)
        {
            try
            {
                Log.Information(Common.Constants.LogMessageTemplate, nameof(VersionCheck), nameof(BackgroundGetGitHubVersion), "Starting Background Version Check");
                _isCheckRunning = true;
                UpdateStartingCallback?.Invoke(this, null);

                //give DaxStudio a little time to get started up so we don't impede work people are doing with this version check
                if (_isAutomaticCheck)
                {
                    System.Threading.Thread.Sleep(CHECK_SECONDS_AFTER_STARTUP.SecondsToMilliseconds());
                    _isAutomaticCheck = false;
                }
                LastVersionCheck = DateTime.UtcNow;

                //VersionStatus = "Checking for updates...";
                //NotifyOfPropertyChange(() => VersionStatus);
                try
                {
                    if (_webRequestFactory == null)
                    {
                        Log.Information(Common.Constants.LogMessageTemplate, nameof(VersionCheck), nameof(BackgroundGetGitHubVersion), "Creating WebRequestFactory");
                        _webRequestFactory = WebRequestFactory.CreateAsync(_globalOptions, _eventAggregator).Result;
                    }
                    Log.Information(Common.Constants.LogMessageTemplate, nameof(VersionCheck), nameof(BackgroundGetGitHubVersion), "Starting Population of version information from Github");
                    PopulateServerVersionFromGithub(_webRequestFactory);
                    Log.Information(Common.Constants.LogMessageTemplate, nameof(VersionCheck), nameof(BackgroundGetGitHubVersion), "Updating Version Status");

                }
                catch (Exception ex)
                {
                    Log.Error("{class} {method} {error}", "VersionCheck", "worker_DoWork", ex.Message);
                    _eventAggregator.PublishOnUIThread(new ErrorEventArgs(ex));
                }

                CheckVersion();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(VersionCheck), nameof(BackgroundGetGitHubVersion), ex.Message);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, $"Error while checking for updates: {ex.Message}"));
            }
            finally
            {
                _isCheckRunning = false;
                UpdateCompleteCallback?.Invoke(this, null);
            }
            Log.Information(Common.Constants.LogMessageTemplate, nameof(VersionCheck), nameof(BackgroundGetGitHubVersion), "Finished Background Version Check");
        }

        public void CheckVersion()
        {
            try
            {
                if (!VersionIsLatest && ServerVersion != DismissedVersion && !ServerVersion.IsNotSet())
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
                if (_globalOptions.LastVersionCheckUTC == DateTime.MinValue) _globalOptions.LastVersionCheckUTC = DateTime.UtcNow;
                return _globalOptions.LastVersionCheckUTC;
            }
            set => _globalOptions.LastVersionCheckUTC = value;
        }


        public Version DismissedVersion
        {
            get => _globalOptions.DismissedVersion;
            set => _globalOptions.DismissedVersion = value;
        }

        public Version LocalVersion => typeof(VersionCheck).Assembly.GetName().Version;
         
        public int LocalBuild =>  typeof(VersionCheck).Assembly.GetName().Version.Build;

        public Version ServerVersion => _globalOptions.CurrentDownloadVersion;

        //private void SetVersionStatus()
        //{
        //    if (ServerVersion.IsNotSet() )
        //    { VersionStatus = "(Unable to get version information)"; }
        //    else if (LocalVersion.CompareTo(ServerVersion) > 0)
        //    { VersionStatus = string.Format("(Ahead of {1} Version - {0} )", ServerVersion.ToString(3), ServerVersionType); }
        //    else if (LocalVersion.CompareTo(ServerVersion) == 0)
        //    { VersionStatus = string.Format("(Latest {0} Version)", ServerVersionType); }
        //    else
        //    { VersionStatus = string.Format("(New {1} Version available - {0})", ServerVersion.ToString(3), ServerVersionType); }

        //    UpdateCompleteCallback();

        //    NotifyOfPropertyChange(() => VersionStatus);
        //}


        // This code runs async in a background worker
        private void PopulateServerVersionFromGithub(WebRequestFactory wrf)
        {
            Log.Information(Common.Constants.LogMessageTemplate, nameof(VersionCheck), nameof(PopulateServerVersionFromGithub), "Start");

            using (System.Net.WebClient http = wrf.CreateWebClient())
            {
                
                string json = "";

                try
                {
                    //#if DEBUG
                    //                    json = File.ReadAllText(@"..\..\..\src\CurrentReleaseVersion.json");
                    //#else
                    Log.Information(Common.Constants.LogMessageTemplate, nameof(VersionCheck), nameof(PopulateServerVersionFromGithub), "Starting download of CurrentVersion.json");
                    json = http.DownloadString(new Uri(WebRequestFactory.CurrentGithubVersionUrl));
//#endif           
                }
                catch (System.Net.WebException wex)
                {
                    if (wex.Status == System.Net.WebExceptionStatus.ProtocolError &&  ((HttpWebResponse)wex.Response).StatusCode == HttpStatusCode.ProxyAuthenticationRequired )
                    {
                        Log.Information(Common.Constants.LogMessageTemplate, nameof(VersionCheck), nameof(PopulateServerVersionFromGithub), "Re-trying download of CurrentVersion.json with proxy auth");
                        // assume proxy auth error and re-try with current user credentials
                        http.Proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
                        json = http.DownloadString(new Uri(WebRequestFactory.CurrentGithubVersionUrl));
                    }
                    else
                    {
                        throw;
                    }
                }

                JObject jobj = JObject.Parse(json);
                try
                {
                    _productionVersion = Version.Parse((string)jobj["Version"]);
                    _productionDownloadUrl = new Uri((string)jobj["DownloadUrl"]);

                    ServerVersionType = "Production";
                    _globalOptions.CurrentDownloadVersion = _productionVersion;
                    DownloadUrl = _productionDownloadUrl;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(VersionCheck), nameof(PopulateServerVersionFromGithub), $"Error parsing CurrentVersion.json: {ex.Message}");
                    _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, $"The following error occurred while checking if there is an updated release available: {ex.Message}"));
                }
                finally
                {
                    UpdateCompleteCallback?.Invoke(this, null);
                }
                Log.Information(Common.Constants.LogMessageTemplate, nameof(VersionCheck), nameof(PopulateServerVersionFromGithub), "Finish");
            }
        }

        public string ServerVersionType {
            get => _serverVersionType;
            set {
                _serverVersionType = value;
                NotifyOfPropertyChange(() => ServerVersionType);
            }
        }

        public bool VersionIsLatest
        {
            get
            {
                if (ServerVersion.IsNotSet()) return false;
                return LocalVersion.CompareTo(ServerVersion) >= 0;
            }
        }

        //public string VersionStatus { get; set; }

        public void OpenDaxStudioReleasePageInBrowser()
        {
            // Open URL in Browser
            System.Diagnostics.Process.Start(DownloadUrl.ToString());
        }

        public void Update()
        {
            //if (!ServerVersion.IsNotSet()) return;
            //var ver = this.ServerVersion;
            //CheckVersion();
            if (_isCheckRunning) return;
            worker.RunWorkerAsync();
        }



        public Uri DownloadUrl { 
            get { return _downloadUrl; } 
            set { if (value == _downloadUrl) return; 
                _downloadUrl = value; 
                NotifyOfPropertyChange(() => DownloadUrl); 
            } 
        }


        public event EventHandler UpdateCompleteCallback;
        public event EventHandler UpdateStartingCallback;
    }
}

