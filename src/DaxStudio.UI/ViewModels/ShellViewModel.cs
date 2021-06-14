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
using System.Windows.Input;
using DaxStudio.UI.Interfaces;
using System.Windows.Media;
using DaxStudio.UI.Extensions;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(IShell))]
    public class ShellViewModel :
        Screen,
        IShell,
        IHandle<NewVersionEvent>,
        IHandle<AutoSaveEvent>,
        IHandle<StartAutoSaveTimerEvent>,
        IHandle<StopAutoSaveTimerEvent>,
        IHandle<ChangeThemeEvent>,
        IHandle<UpdateHotkeys>,
        IHandle<UpdateGlobalOptions>
    {

        private readonly IEventAggregator _eventAggregator;
        private readonly IDaxStudioHost _host;
        private NotifyIcon _notifyIcon;
        private Window _window;
        private readonly string _username;
        private readonly DateTime _utcSessionStart;

        private InputBindings _inputBindings;

        //private ILogger log;
        [ImportingConstructor]
        public ShellViewModel(IEventAggregator eventAggregator
                            , RibbonViewModel ribbonViewModel
                            , StatusBarViewModel statusBar
                            , IConductor conductor
                            , IDaxStudioHost host
                            , IVersionCheck versionCheck
                            , IGlobalOptions options
                            , IAutoSaver autoSaver
                            , IThemeManager themeManager)
        {
            Log.Debug(Constants.LogMessageTemplate, nameof(ShellViewModel), "ctor", "Starting Constructor");
            _utcSessionStart = DateTime.UtcNow;
            Ribbon = ribbonViewModel;
            Ribbon.Shell = this;
            StatusBar = statusBar;
            Options = options;
            AutoSaver = autoSaver;
            ThemeManager = themeManager;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);

            Tabs = (DocumentTabViewModel)conductor;
            Tabs.ConductWith(this);
            //Tabs.CloseStrategy = new ApplicationCloseStrategy();
            Tabs.CloseStrategy = IoC.Get<ApplicationCloseAllStrategy>();
            _host = host;
            _username = UserHelper.GetUser();
            var recoveringFiles = false;

            // get master auto save indexes and only get crashed index files...
            var autoSaveInfo = AutoSaver.LoadAutoSaveMasterIndex();
            var filesToRecover = autoSaveInfo.Values.Where(idx => idx.IsCurrentVersion && idx.ShouldRecover).SelectMany(entry => entry.Files);

            // check for auto-saved files and offer to recover them
            if (filesToRecover.Any())
            {
                Log.Debug(Constants.LogMessageTemplate, nameof(ShellViewModel), "ctor", "Found auto-save files, beginning recovery");
                recoveringFiles = true;
                RecoverAutoSavedFiles(autoSaveInfo);
            }
            else
            {
                // if there are no auto-save files to recover, start the auto save timer
                Log.Debug(Constants.LogMessageTemplate, nameof(ShellViewModel), "ctor", "Starting auto-save timer");
                eventAggregator.PublishOnUIThreadAsync(new StartAutoSaveTimerEvent());
            }

            // if a filename was passed in on the command line open it
            if (!string.IsNullOrEmpty(_host.CommandLineFileName))
            {
                Log.Debug(Constants.LogMessageTemplate, nameof(ShellViewModel), "ctor", $"Opening file from command line: '{_host.CommandLineFileName}'");
                Tabs.NewQueryDocument(_host.CommandLineFileName);
            }

            // if no tabs are open at this point and we are not recovering auto-save file then, open a blank document
            if (Tabs.Items.Count == 0 && !recoveringFiles)
            {
                Log.Debug(Constants.LogMessageTemplate, nameof(ShellViewModel), "ctor", "Opening a new blank query window");
                NewDocument();
            }


            VersionChecker = versionCheck;
            VersionChecker.UpdateCompleteCallback += VersionCheckComplete;

            DisplayName = AppTitle;

            Application.Current.Activated += OnApplicationActivated;
            

            AutoSaveTimer = new Timer(Constants.AutoSaveIntervalMs);
            AutoSaveTimer.Elapsed += AutoSaveTimerElapsed;

            Log.Debug("============ Shell Started - v{version} =============", Version.ToString());
            
        }

        private void VersionCheckComplete(object sender, EventArgs e)
        {
            NotifyOfPropertyChange(nameof(IsUpdateAvailable));
            NotifyOfPropertyChange(nameof(UpdateMessage));
        }

        private IThemeManager ThemeManager { get; }

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
                // firing access denied errors on the auto-save file. Once the UI thread is free 
                // the initial request will continue and the timer will be re-enabled.
              
                AutoSaveTimer.Enabled = false;
                
                await AutoSaver.Save(Tabs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // we just catch and log any errors, we don't want the auto-save timer to be
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
            _eventAggregator.PublishOnUIThread(new ApplicationActivatedEvent());
            System.Diagnostics.Debug.WriteLine("OnApplicationActivated");
        }

        
        public IAutoSaver AutoSaver { get; }
        public Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public DocumentTabViewModel Tabs { get; set; }
        public RibbonViewModel Ribbon { get; set; }
        public StatusBarViewModel StatusBar { get; set; }
        public IGlobalOptions Options { get; }

        public IVersionCheck VersionChecker { get; set; }
        
        public bool IsUpdateAvailable => !VersionChecker.VersionIsLatest;
        public string UpdateMessage => $"Click to open the download page for version {VersionChecker.ServerVersion.ToString(3)}";

        public void UpdateFlagClick()
        {
            // Open URL in Browser
            System.Diagnostics.Process.Start( VersionChecker.DownloadUrl.ToString());
        }
        
        public override void TryClose(bool? dialogResult = null)
        {
            //Properties.Settings.Default.Save();
            base.TryClose(dialogResult);
            if (dialogResult != false )
            {
                Ribbon.OnClose();
                _notifyIcon?.Dispose();
                AutoSaveTimer.Enabled = false;
                if (!Application.Current.Properties.Contains("HasCrashed") )
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

        
        protected override  void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            // load the saved window positions
            _window = view as Window;
            if (_window != null)
            {
                _window.Closing += WindowClosing;
                // SetPlacement will adjust the position if it's outside of the visible boundaries
                _window.SetPlacement(Options.WindowPosition);
                _notifyIcon = new NotifyIcon(_window, _eventAggregator);
                if (_host.DebugLogging) ShowLoggingEnabledNotification();

                //Application.Current.LoadRibbonTheme();
                _inputBindings = new InputBindings(_window);
            }

            _inputBindings.RegisterCommands(GetInputBindingCommands());
            _eventAggregator.PublishOnBackgroundThread(new LoadQueryHistoryAsyncEvent());
            
        }

        private IEnumerable<InputBindingCommand> GetInputBindingCommands()
        {
            // load custom key bindings from Options
            yield return new InputBindingCommand(this, nameof(CommentSelection), Options.HotkeyCommentSelection);
            yield return new InputBindingCommand(this, nameof(RunQuery), Options.HotkeyRunQuery);
            yield return new InputBindingCommand(this, nameof(RunQuery), Options.HotkeyRunQueryAlt);
            yield return new InputBindingCommand(this, nameof(NewDocument), Options.HotkeyNewDocument);
            yield return new InputBindingCommand(this, nameof(NewDocumentWithCurrentConnection), Options.HotkeyNewDocumentWithCurrentConnection);
            yield return new InputBindingCommand(this, nameof(OpenDocument), Options.HotkeyOpenDocument);
            yield return new InputBindingCommand(this, nameof(SaveCurrentDocument), Options.HotkeySaveDocument);
            yield return new InputBindingCommand(this, nameof(SelectionToUpper), Options.HotkeyToUpper);
            yield return new InputBindingCommand(this, nameof(SelectionToLower), Options.HotkeyToLower);
            yield return new InputBindingCommand(this, nameof(UncommentSelection), Options.HotkeyUnCommentSelection);
            yield return new InputBindingCommand(this, nameof(Redo), "Ctrl + Y");
            yield return new InputBindingCommand(this, nameof(Undo), "Ctrl + Z");
            yield return new InputBindingCommand(this, nameof(Undo), "Alt + Delete");
            yield return new InputBindingCommand(this, nameof(SwapDelimiters), "Ctrl + OemSemiColon");
            yield return new InputBindingCommand(this, nameof(SwapDelimiters), "Ctrl + OemComma");
            yield return new InputBindingCommand(this, nameof(Find), "F3");
            yield return new InputBindingCommand(this, nameof(FindPrev), "Shift + F3");
            yield return new InputBindingCommand(this, nameof(FormatQueryStandard), Options.HotkeyFormatQueryStandard);
            yield return new InputBindingCommand(this, nameof(FormatQueryAlternate), Options.HotkeyFormatQueryAlternate);
            yield return new InputBindingCommand(this, nameof(GotoLine), Options.HotkeyGotoLine);
            yield return new InputBindingCommand(this, nameof(ToggleComment), Options.HotkeyToggleComment);
            yield return new InputBindingCommand(this, nameof(SelectWord), Options.HotkeySelectWord);
            yield return new InputBindingCommand(this, nameof(MoveLineUp), "Ctrl + OemPlus");
            yield return new InputBindingCommand(this, nameof(MoveLineUp), "Ctrl + Add");
            yield return new InputBindingCommand(this, nameof(MoveLineDown), "Ctrl + OemMinus");
            yield return new InputBindingCommand(this, nameof(MoveLineDown), "Ctrl + Subtract");

        }

        public void ResetInputBindings()
        {
            _inputBindings.DeregisterCommands();
            _inputBindings.RegisterCommands(GetInputBindingCommands());
        }

        void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            double sessionMin = 0;
            try
            {
                TimeSpan sessionSpan = DateTime.UtcNow - _utcSessionStart;
                sessionMin = sessionSpan.TotalMinutes;
            }
            catch
            {
                // swallow all errors
            }

            if (Options.AnyExternalAccessAllowed())
            {
                Telemetry.TrackEvent("App.Shutdown", new Dictionary<string, string>
                {
                    {"SessionMin", sessionMin.ToString("#")}
                });
                Telemetry.Flush();
            }

            // Store the current window position
            var w = sender as Window;
            Options.WindowPosition = w.GetPlacement();
            _window.Closing -= WindowClosing;

        }

        public override void CanClose(Action<bool> callback)
        {
            Tabs.CanClose(callback);
        }

        #region Event Handlers
        public void Handle(NewVersionEvent message)
        {           
            var newVersionText =
                $"Version {message.NewVersion.ToString(3)} is available for download.\nClick here to go to the download page";
            Log.Debug("{class} {method} {message}", "ShellViewModel", "Handle<NewVersionEvent>", newVersionText);
            _notifyIcon.Notify(newVersionText, message.DownloadUrl.ToString());
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
                var loggingText = "Debug Logging enabled.\nClick here to open the log folder";
                var fullPath = ApplicationPaths.LogPath;
                _notifyIcon.Notify(loggingText, fullPath);
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

        public bool IsOverlayVisible => _overlayDependencies > 0;

        public object UserString => Options.ShowUserInTitlebar? $" ({_username})":string.Empty;

        public string AppTitle { get {
#if PREVIEW
                return string.Format("DaxStudio - {0} (PREVIEW){1}", Version.ToString(4),UserString);
#else
                return $"DaxStudio - {Version.ToString(3)}{UserString}";
#endif    
            }
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

        public void ToggleComment()
        {
            _eventAggregator.PublishOnUIThread(new ToggleCommentEvent());
        }

        public void SelectWord()
        {
            _eventAggregator.PublishOnUIThread(new EditorHotkeyEvent( EditorHotkey.SelectWord));
        }

        public void MoveLineUp()
        {
            try
            {
                _eventAggregator.PublishOnUIThread(new EditorHotkeyEvent(EditorHotkey.MoveLineUp));
            }
            catch(Exception ex)
            {
                var msg = $"Error moving editor line up: {ex.Message}";
                Log.Error(ex, nameof(ShellViewModel), nameof(MoveLineUp), msg);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, msg));
            }
        }
        public void MoveLineDown()
        {
            try
            {
                _eventAggregator.PublishOnUIThread(new EditorHotkeyEvent(EditorHotkey.MoveLineDown));
            }
            catch (Exception ex)
            {
                var msg = $"Error moving editor line down: {ex.Message}";
                Log.Error(ex, nameof(ShellViewModel), nameof(MoveLineDown), msg);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, msg));
            }
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
            ThemeManager.SetTheme(message.Theme);
            //if (message.Theme == "Dark") SetDarkTheme();
            //else SetLightTheme();
        }


        public void Handle(UpdateHotkeys message)
        {
            ResetInputBindings();
        }

        public void Handle(UpdateGlobalOptions message)
        {
            // force a refresh of the User string in case this was just turned on in the options
            DisplayName = AppTitle;
        }


        #endregion


    }


}