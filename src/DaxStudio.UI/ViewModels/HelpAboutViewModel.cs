using System;
using Caliburn.Micro;
using System.Collections.Generic;
using DaxStudio.Interfaces;
using System.ComponentModel.Composition;
using DaxStudio.Common;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Extensions;
using Humanizer;
using System.Reflection;
using System.Linq;
using Serilog;

namespace DaxStudio.UI.ViewModels
{
    [Export]
    public class HelpAboutViewModel : Screen
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IDaxStudioHost _host;

        public IGlobalOptions Options { get; }

        [ImportingConstructor]
        public HelpAboutViewModel(IEventAggregator eventAggregator, IVersionCheck checker, IDaxStudioHost host, IGlobalOptions options) {
            _eventAggregator = eventAggregator;
            _host = host;
            Options = options;
            DisplayName = "About DaxStudio";
            CheckingUpdateStatus = true;
          
            NotifyOfPropertyChange(() => UpdateStatus);

            // start version check async
            VersionChecker = checker;
            VersionChecker.UpdateStartingCallback += this.VersionUpdateStarting;
            VersionChecker.UpdateCompleteCallback += this.VersionUpdateComplete;

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetEntryAssembly();
            Version version = assembly?.GetName().Version;

            BuildNumber = version?.ToString();
            FullVersionNumber = version?.ToString(3);

            try
            {
                var dateStr = GetResourceFromAssembly(assembly, "BuildDate.txt").Normalize();
                BuildDate = DateTime.Parse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind).ToString(Constants.BuildDateFormat);

            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(HelpAboutViewModel), "ctor", "Error reading Build Date from .exe resource");
                BuildDate = "<n/a>";
            }


            //VersionChecker.PropertyChanged += VersionChecker_PropertyChanged;
            //Task.Run(() => 
            //    {
            //        this.VersionChecker.Update(); 
            //    })
            //    .ContinueWith((previous)=> {
            //        //  checking for exceptions and log them
            //        if (previous.IsFaulted)
            //        {
            //            Log.Error(previous.Exception, "{class} {method} {message}", nameof(HelpAboutViewModel), "ctor", $"Error updating version information: {previous.Exception.Message}");
            //            _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, "Unable to check for an updated release on github"));
            //            CheckingUpdateStatus = false;
            //            NotifyOfPropertyChange(() => CheckingUpdateStatus);
            //            return;
            //        }

            //        CheckingUpdateStatus = false;
            //        UpdateStatus = VersionChecker.VersionStatus;
            //        VersionIsLatest = VersionChecker.VersionIsLatest;
            //        DownloadUrl = VersionChecker.DownloadUrl;
            //        NotifyOfPropertyChange(() => VersionIsLatest);
            //        NotifyOfPropertyChange(() => DownloadUrl);
            //        NotifyOfPropertyChange(() => UpdateStatus);
            //        NotifyOfPropertyChange(() => CheckingUpdateStatus);
            //    },TaskScheduler.Default);
        }

        public void VersionUpdateStarting(object sender, EventArgs e)
        {
            IsCheckRunning = true;
        }

        public void VersionUpdateComplete(object sender, EventArgs e)
        {
            IsCheckRunning = false;

            NotifyOfPropertyChange(() => UpdateStatus);
            NotifyOfPropertyChange(() => LastChecked);
            VersionIsLatest = VersionChecker.VersionIsLatest;
            //DownloadUrl = VersionChecker.DownloadUrl;
            NotifyOfPropertyChange(() => VersionIsLatest);
            NotifyOfPropertyChange(() => DownloadUrl);
        }

        //void VersionChecker_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        //{
        //    if (e.PropertyName == "VersionStatus")
        //    {

        //        VersionUpdateComplete();
        //    }
        //}

        public string FullVersionNumber { get; }
        
        public string BuildNumber { get; }
        
        public string BuildDate { get; }

        //[Import(typeof(IVersionCheck))]
        public IVersionCheck VersionChecker { get; }

        public SortedList<string,string> ReferencedAssemblies
        {
            get
            {
                var l = new SortedList<string,string>();
                var ass = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (var a in ass.GetReferencedAssemblies())
                {
                    if (!l.ContainsKey(a.Name))
                    {
                        l.Add(a.Name, a.Version.ToString());
                    }
                }
                return l; 
            }
        }
        public async void Ok()
        {
            await TryCloseAsync();
        }

        public bool CheckingUpdateStatus
        {
            get;
            set;
        }

        private bool _isCheckRunning;
        public bool IsCheckRunning { get => _isCheckRunning;
            set {
                _isCheckRunning = value;
                NotifyOfPropertyChange(() => IsCheckRunning);
            } 
        }

        public string UpdateStatus
        {
            get
            {
                if (IsCheckRunning)
                {
                    return "Checking for updates...";
                }

                if (VersionChecker.ServerVersion.IsNotSet() || Options.BlockVersionChecks) return "(Unable to get version information from daxstudio.org)"; 

                var versionComparison = VersionChecker.LocalVersion.CompareTo(VersionChecker.ServerVersion);
                if (versionComparison > 0)  return $"a preview release"; 
                if (versionComparison == 0) return "up to date"; 
                    
                return $"out dated";

            }

        }

        public string LastChecked
        {
            get
            {
                if (Options.BlockVersionChecks)
                    return "Version checks blocked in options";
                return $"Last checked {Options.LastVersionCheckUTC.ToUniversalTime().Humanize()}";
            }
        }

        public Uri DownloadUrl => VersionChecker.DownloadUrl;
        public bool VersionIsLatest { get; private set; }

        public bool IsLoggingEnabled { get { return _host.DebugLogging; } }

        public string LogFolder { get { return @"file:///" + ApplicationPaths.LogPath; } }


        private string GetResourceFromAssembly(Assembly assembly, string resourceName)
        {
            if (assembly == null) return DateTime.UtcNow.ToString("o");

            var names = assembly.GetManifestResourceNames();
            var name = names.FirstOrDefault(n => n.EndsWith(resourceName));
            //string ns = "DaxStudio.Standalone";
            //string name = String.Format("{0}.MyDocuments.Document.pdf", ns);
            using (var stream = assembly.GetManifestResourceStream(name))
            {
                if (stream == null) return null;
                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);
                var str = System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                return str;
            }
        }
    }

}
