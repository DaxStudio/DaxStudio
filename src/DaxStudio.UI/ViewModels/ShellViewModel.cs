using System;
using System.Reflection;
using Caliburn.Micro;
using System.ComponentModel.Composition;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Utils;
using DaxStudio.UI.Model;
using Serilog;
using System.Windows;
using DaxStudio.Common;
using System.Timers;
using System.Linq;
using System.Collections.Generic;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof (IShell))]
    public class ShellViewModel : 
        Screen, 
        IShell,
        IHandle<NewVersionEvent>,
        IHandle<AutoSaveEvent>,
        IHandle<StartAutoSaveTimerEvent>,
        IHandle<StopAutoSaveTimerEvent>,
        IHandle<ChangeThemeEvent>
    {
        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDaxStudioHost _host;
        private NotifyIcon notifyIcon;
        private Window _window;
        private readonly Application _app;
        private InputBindings _inputBindings;
        
        //private ILogger log;
        [ImportingConstructor]
        public ShellViewModel(IWindowManager windowManager
                            , IEventAggregator eventAggregator 
                            , RibbonViewModel ribbonViewModel
                            , StatusBarViewModel statusBar
                            , IConductor conductor
                            , IDaxStudioHost host
                            , IVersionCheck versionCheck
                            , IGlobalOptions options
                            , IAutoSaver autoSaver
                            )
        {

            Ribbon = ribbonViewModel;
            Ribbon.Shell = this;
            StatusBar = statusBar;
            Options = options;
            AutoSaver = autoSaver;
            _windowManager = windowManager;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            Tabs = (DocumentTabViewModel) conductor;
            Tabs.ConductWith(this);
            //Tabs.CloseStrategy = new ApplicationCloseStrategy();
            Tabs.CloseStrategy = IoC.Get<ApplicationCloseAllStrategy>();
            _host = host;
            _app = Application.Current;
            var recoveringFiles = false;

            // get master auto save indexes and only get crashed index files...
            var autoSaveInfo = AutoSaver.LoadAutoSaveMasterIndex();
            var filesToRecover = autoSaveInfo.Values.Where(idx => idx.IsCurrentVersion && idx.ShouldRecover).SelectMany(entry => entry.Files);
            // check for auto-saved files and offer to recover them
            if (filesToRecover.Any())
            {
                recoveringFiles = true;
                RecoverAutoSavedFiles(autoSaveInfo);
            }
            else
            {   
                // if there are no auto-save files to recover, start the auto save timer
                eventAggregator.PublishOnUIThreadAsync(new StartAutoSaveTimerEvent());
            }

            // if a filename was passed in on the command line open it
            if (!string.IsNullOrEmpty(_host.CommandLineFileName)) Tabs.NewQueryDocument(_host.CommandLineFileName);

            // if no tabs are open at this point and we are not recovering autosave file then, open a blank document
            if (Tabs.Items.Count == 0 && !recoveringFiles) NewDocument();


            VersionChecker = versionCheck;

#if PREVIEW
            DisplayName = string.Format("DaxStudio - {0} (PREVIEW)", Version.ToString(4));
#else
            DisplayName = string.Format("DaxStudio - {0}", Version.ToString(3));
#endif
            Application.Current.Activated += OnApplicationActivated; 
            Log.Verbose("============ Shell Started - v{version} =============",Version.ToString());

            AutoSaveTimer = new Timer(Constants.AutoSaveIntervalMs);
            AutoSaveTimer.Elapsed += new ElapsedEventHandler(AutoSaveTimerElapsed);
            
        }

        private Timer AutoSaveTimer { get; }

        private void RecoverAutoSavedFiles(Dictionary<int,AutoSaveIndex> autoSaveInfo)
        {
            Log.Information("{class} {method} {message}", "ShellViewModel", "RecoverAutoSavedFiles", $"Found {autoSaveInfo.Values.Count} auto save index files");
            // show recovery dialog
            _eventAggregator.PublishOnUIThreadAsync(new AutoSaveRecoveryEvent(autoSaveInfo));
            
        }

        private async void AutoSaveTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                // disable the timer while we are saving, so that if access to the UI thread is 
                // blocked and we cannot read the contents of the editor controls we do not keep
                // firing access denied errors on the autosave file. Once the UI thread is free 
                // the initial request will continue and the timer will be re-enabled.
              
                AutoSaveTimer.Enabled = false;
                
                await AutoSaver.Save(Tabs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // we just catch and log any errors, we don't want the autosave timer to be
                // the cause of any crashes itself
                Log.Error(ex, "{class} {method} {message}", "ShellViewModel", "AutoSaveTimerElapsed", ex.Message);
            }
            finally
            {
                AutoSaveTimer.Enabled = true;
            }
        }

        private void OnApplicationActivated(object sender, EventArgs e)
        {
            Log.Debug("{class} {method}", "ShellViewModel", "OnApplicationActivated");
            _eventAggregator.PublishOnUIThreadAsync(new ApplicationActivatedEvent());
            System.Diagnostics.Debug.WriteLine("OnApplicationActivated");
        }

        
        public IAutoSaver AutoSaver { get; }
        public Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        public DocumentTabViewModel Tabs { get; set; }
        public RibbonViewModel Ribbon { get; set; }
        public StatusBarViewModel StatusBar { get; set; }
        public IGlobalOptions Options { get; }
        public ISettingProvider SettingProvider { get; }

        public void ContentRendered()
        { }

        public IVersionCheck VersionChecker { get; set; }
        public override void TryClose(bool? dialogResult = null)
        {
            //Properties.Settings.Default.Save();
            base.TryClose(dialogResult);
            if (dialogResult != false )
            {
                Ribbon.OnClose();
                notifyIcon?.Dispose();
                AutoSaveTimer.Enabled = false;
                AutoSaver.RemoveAll();
            }
        }
        //public override void TryClose()
        //{
        //    base.TryClose();
        //}

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
            TryClose();
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            _eventAggregator.PublishOnUIThread(new ApplicationActivatedEvent());
        }

        
        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            // load the saved window positions
            _window = view as Window;
            _window.Closing += windowClosing;
            // SetPlacement will adjust the position if it's outside of the visible boundaries
            //_window.SetPlacement(Properties.Settings.Default.MainWindowPlacement);
            _window.SetPlacement(Options.WindowPosition);
            notifyIcon = new NotifyIcon(_window);
            if (_host.DebugLogging) ShowLoggingEnabledNotification();

            //Application.Current.LoadRibbonTheme();
            _inputBindings = new InputBindings(_window);
            _inputBindings.RegisterCommands(GetInputBindingCommands());
        }

        private IEnumerable<InputBindingCommand> GetInputBindingCommands()
        {
            // TODO - we should load custom key bindings from Options
            yield return new InputBindingCommand(this, nameof(CommentSelection), "Ctrl+Alt C");
            
        }

        void windowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Store the current window position
            var w = sender as Window;
            //Properties.Settings.Default.MainWindowPlacement = w.GetPlacement();
            //Properties.Settings.Default.Save();
            Options.WindowPosition = w.GetPlacement();
            _window.Closing -= windowClosing;

        }

        public override void CanClose(Action<bool> callback)
        {
            Tabs.CanClose(callback);
        }

        #region Event Handlers
        public void Handle(NewVersionEvent message)
        {           
            var newVersionText = string.Format("Version {0} is available for download.\nClick here to go to the download page",message.NewVersion.ToString(3));
            Log.Debug("{class} {method} {message}", "ShellViewModel", "Handle<NewVersionEvent>", newVersionText);
            notifyIcon.Notify(newVersionText, message.DownloadUrl.ToString());
        }

        public void Handle(AutoSaveEvent message)
        {
            AutoSaver.Save(Tabs).AsResult();
        }
        #endregion

        public void ShowLoggingEnabledNotification()
        {
            try
            {
                var loggingText = string.Format("Debug Logging enabled.\nClick here to open the log folder");
                var fullPath = ApplicationPaths.LogPath;
                notifyIcon.Notify(loggingText, fullPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error Showing Notify Icon {0}", ex.Message);
            }
        }

#region Overlay code
        private int _overlayDependencies;
        public void ShowOverlay()
        {
            _overlayDependencies++;
            NotifyOfPropertyChange(() => IsOverlayVisible);
        }

        public void HideOverlay()
        {
            _overlayDependencies--;
            NotifyOfPropertyChange(() => IsOverlayVisible);
        }

        public bool IsOverlayVisible
        {
            get { return _overlayDependencies > 0; }
        }
#endregion

#region Global Keyboard Hooks
        public void RunQuery()
        {
            Ribbon.RunQuery();
        }

        public void SaveCurrentDocument()
        {
            Ribbon.Save();
        }

        public void NewDocument()
        {
            _eventAggregator.PublishOnUIThread(new NewDocumentEvent(Ribbon.SelectedTarget));
        }

        public void NewDocumentWithCurrentConnection()
        {
            _eventAggregator.PublishOnUIThread(new NewDocumentEvent(Ribbon.SelectedTarget,Ribbon.ActiveDocument));
        }

        public void OpenDocument()
        {
            _eventAggregator.PublishOnUIThread(new OpenFileEvent() );
        }

        public void SelectionToUpper()
        {
            _eventAggregator.PublishOnUIThread(new SelectionChangeCaseEvent(ChangeCase.ToUpper));
        }

        public void SelectionToLower()
        {
            _eventAggregator.PublishOnUIThread(new SelectionChangeCaseEvent(ChangeCase.ToLower));
        }

        public void UncommentSelection()
        {
            _eventAggregator.PublishOnUIThread(new CommentEvent(false));
        }

        public void CommentSelection()
        {
            _eventAggregator.PublishOnUIThread(new CommentEvent(true));
        }

        public void Undo()
        {
            Ribbon.Undo();
        }

        public void Redo()
        {
            Ribbon.Redo();
        }

        public void GotoLine()
        {
            Ribbon.GotoLine();
        }

        public void Find()
        {
            Ribbon.FindNow();
        }

        public void FindPrev()
        {
            Ribbon.FindPrevNow();
        }

        public void FormatQueryAlternate()
        {
            Ribbon.FormatQueryAlternate();
        }
        public void FormatQueryStandard()
        {
            Ribbon.FormatQueryStandard();
        }

        public void SwapDelimiters()
        {
            Ribbon.SwapDelimiters();
        }

        public void HotKey(object param)
        {
            System.Diagnostics.Debug.WriteLine("HotKey" + param.ToString());
        }
        #endregion

        #region Event Aggregator methods
        public void Handle(StartAutoSaveTimerEvent message)
        {
            Log.Information("{class} {method} {message}", "ShellViewModel", "Handle<StartAutoSaveTimer>", "AutoSave Timer Starting");
            AutoSaveTimer.Enabled = true;
        }

        public void Handle(StopAutoSaveTimerEvent message)
        {
            Log.Information("{class} {method} {message}", "ShellViewModel", "Handle<StopAutoSaveTimer>", "AutoSave Timer Stopping");
            AutoSaveTimer.Enabled = false;
        }

        public void Handle(ChangeThemeEvent message)
        {
            if (message.Theme == "Dark") SetDarkTheme();
            else SetLightTheme();
        }

        private void SetLightTheme()
        {
            _app.LoadLightTheme();
        }

        private void SetDarkTheme()
        {
            _app.LoadDarkTheme();
        }


        #endregion


    }


}