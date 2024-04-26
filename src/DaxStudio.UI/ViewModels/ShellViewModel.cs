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
using System.Security.Principal;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using DaxStudio.UI.Views;
using System.Windows.Interop;
using common = DaxStudio.Common;
using DaxStudio.Common.Extensions;
using GongSolutions.Wpf.DragDrop;
using Windows.ApplicationModel.VoiceCommands;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(IShell))]
    public class ShellViewModel :
        Screen,
        IShell,
        IDisposable,
        IDropTarget,
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
            _eventAggregator.SubscribeOnPublishedThread(this);

            Tabs = (DocumentTabViewModel)conductor;
            Tabs.ConductWith(this);
            //Tabs.CloseStrategy = new ApplicationCloseStrategy();
            Tabs.CloseStrategy = IoC.Get<ApplicationCloseAllStrategy>();
            _host = host;
            _username = UserHelper.GetUser();


            VersionChecker = versionCheck;
            VersionChecker.UpdateCompleteCallback += VersionCheckComplete;

            DisplayName = AppTitle;

            Application.Current.Activated += OnApplicationActivated;

            AutoSaveTimer = new System.Timers.Timer(Constants.AutoSaveIntervalMs);
            AutoSaveTimer.Elapsed += AutoSaveTimerElapsed;

            Log.Debug("============ Shell Started - v{version} =============", Version.ToString());
            
        }

        private void VersionCheckComplete(object sender, EventArgs e)
        {
            NotifyOfPropertyChange(nameof(IsUpdateAvailable));
            NotifyOfPropertyChange(nameof(UpdateMessage));
        }

        private IThemeManager ThemeManager { get; }

        private System.Timers.Timer AutoSaveTimer { get; }

        private async Task RecoverAutoSavedFiles(Dictionary<int,AutoSaveIndex> autoSaveInfo)
        {
            Log.Information("{class} {method} {message}", "ShellViewModel", "RecoverAutoSavedFiles", $"Found {autoSaveInfo.Values.Count} auto save index files");
            // show recovery dialog
            await _eventAggregator.PublishOnUIThreadAsync(new AutoSaveRecoveryEvent(autoSaveInfo));
            
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
            _eventAggregator.PublishOnUIThreadAsync(new ApplicationActivatedEvent());
        }

        
        public IAutoSaver AutoSaver { get; }
        public Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public DocumentTabViewModel Tabs { get; set; }
        public RibbonViewModel Ribbon { get; set; }
        public StatusBarViewModel StatusBar { get; set; }
        public IGlobalOptions Options { get; }

        public IVersionCheck VersionChecker { get; set; }
        
        public bool IsUpdateAvailable => !VersionChecker.VersionIsLatest && !Application.Current.Args().NoPreview;
        public string UpdateMessage => $"Click to open the download page for version {VersionChecker.ServerVersion.ToString(3)}";

        public void UpdateFlagClick()
        {
            try
            {
                // Open URL in Browser
                System.Diagnostics.Process.Start(VersionChecker.DownloadUrl.ToString());
            }
            catch (Exception ex){
                Log.Error(ex, Constants.LogMessageTemplate, nameof(ShellViewModel), nameof(UpdateFlagClick), $"Error launching download url: '{VersionChecker?.DownloadUrl?.ToString()}'");
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error,"Unable to open the download url, please go to https://daxstudio.org to get the latest version"));
            }
        }


#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task TryCloseAsync(bool? dialogResult = null)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Log.Information(Constants.LogMessageTemplate, nameof(ShellViewModel), nameof(TryCloseAsync), "Attempting application shutdown");
            //await base.TryCloseAsync(dialogResult);
            if (dialogResult != false )
            {
                Ribbon.OnClose();
                _notifyIcon?.Dispose();
                AutoSaveTimer.Enabled = false;
                ThemeManager.Dispose();
                if (Application.Current == null) {
                    Log.Information(Common.Constants.LogMessageTemplate, nameof(ShellViewModel), nameof(TryCloseAsync), "Current Application is null - clearing AutoSave files");
                    AutoSaver.RemoveAll();
                    return;
                }
                if (!Application.Current.Properties.Contains("HasCrashed"))
                {
                    Log.Information(Common.Constants.LogMessageTemplate, nameof(ShellViewModel), nameof(TryCloseAsync), "Clearing AutoSave files");
                    AutoSaver.RemoveAll();
                    return;
                }
            }
            Log.Information(Common.Constants.LogMessageTemplate, nameof(ShellViewModel), nameof(TryCloseAsync), "Application shutdown cancelled");
            return;
        }
        
        protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            await base.OnDeactivateAsync(close, cancellationToken);
            await TryCloseAsync();
            return;
        }
        
        protected override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            await _eventAggregator.PublishOnUIThreadAsync(new ApplicationActivatedEvent(),cancellationToken);
        }

        HwndSource hwndSource;
        protected override  void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            // load the saved window positions
            _window = view as Window;
            if (_window != null)
            {

                _window.Closing += WindowClosing;
                // SetPlacement will adjust the position if it's outside of the visible boundaries
                Log.Debug(Constants.LogMessageTemplate, nameof(ShellViewModel), nameof(OnViewLoaded), $"Setting Window Placement:\n{Options.WindowPosition}");
                _window.SetPlacement(Options.WindowPosition);
                _notifyIcon = new NotifyIcon(_window, _eventAggregator);
                if (_host.DebugLogging) ShowLoggingEnabledNotification();
                _window.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPaste));
                //Application.Current.LoadRibbonTheme();
                _inputBindings = new InputBindings(_window);

                hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle);
                hwndSource.AddHook(new HwndSourceHook(WndProc));
            }
            else
            {
                Log.Warning(Constants.LogMessageTemplate, nameof(ShellViewModel), nameof(OnViewLoaded), "_window object is null");
            }

            ResetInputBindings();
            _eventAggregator.PublishOnBackgroundThreadAsync(new LoadQueryHistoryAsyncEvent());
            
        }

        static void OnPaste(object target, ExecutedRoutedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("Paste Detected");
            IDataObject obj = Clipboard.GetDataObject();
            var visual = obj.GetData("Power BI Visuals");
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
            yield return new InputBindingCommand(this, nameof(SwapDelimiters), Options.HotkeySwapDelimiters);
            yield return new InputBindingCommand(this, nameof(DebugCommas), Options.HotkeyDebugCommas);
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
            yield return new InputBindingCommand(this, nameof(CopyWithHeaders), "Ctrl + Shift + C");
            yield return new InputBindingCommand(this, nameof(CopySEQuery), Options.HotkeyCopySEQuery);
            yield return new InputBindingCommand(this, nameof(CopyPasteServerTimings), Options.HotkeyCopyPasteServerTimings);
            yield return new InputBindingCommand(this, nameof(CopyPasteServerTimingsData), Options.HotkeyCopyPasteServerTimingsData);
        }

        public void CopyPasteServerTimings()
        {
            _eventAggregator.PublishOnUIThreadAsync(new CopyPasteServerTimingsEvent(includeHeader: true));
        }
        public void CopyPasteServerTimingsData()
        {
            _eventAggregator.PublishOnUIThreadAsync(new CopyPasteServerTimingsEvent(includeHeader: false));
        }
        public void CopySEQuery()
        {
            _eventAggregator.PublishOnUIThreadAsync(new CopySEQueryEvent());
        }

        public void DebugCommas()
        {
            Ribbon.MoveCommasToDebugMode();
        }

        public void CopyWithHeaders()
        {
            _eventAggregator.PublishOnUIThreadAsync(new CopyWithHeadersEvent());
        }

        public void ResetInputBindings()
        {
            try
            {
                _inputBindings.DeregisterCommands();
                _inputBindings.RegisterCommands(GetInputBindingCommands());
            }
            catch (Exception ex)
            {
                var msg = $"Error setting key binding: {ex.Message} Position: {ex.StackTrace}";
                Log.Error(ex, Constants.LogMessageTemplate, nameof(ShellViewModel), nameof(ResetInputBindings), msg);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
            }
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

            if (Options?.AnyExternalAccessAllowed()??false)
            {
                Telemetry.TrackEvent("App.Shutdown", new Dictionary<string, string>
                {
                    {"SessionMin", sessionMin.ToString("#")}
                });
                Telemetry.Flush();
            }
            try
            {
                // Store the current window position
                var w = sender as Window;
                Options.WindowPosition = w.GetPlacement();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(ShellViewModel), nameof(WindowClosing), "Error saving current window position");
            }
            _window.Closing -= WindowClosing;

        }


        public override Task<bool> CanCloseAsync(CancellationToken cancellationToken)
        {
            return Tabs.CanCloseAsync(cancellationToken);
        }

        #region Event Handlers
        public Task HandleAsync(NewVersionEvent message, CancellationToken cancellationToken)
        {
            var newVersionText = $"A new version is available for download.\nClick here to go to the download page";
            try
            {
                newVersionText = $"Version {message.NewVersion.ToString(3)} is available for download.\nClick here to go to the download page";
                Log.Debug("{class} {method} {message}", "ShellViewModel", "Handle<NewVersionEvent>", newVersionText);
                _notifyIcon.Notify(newVersionText, message.DownloadUrl.ToString());
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(ShellViewModel), "Handle<NewVersionEvent>",ex.Message);

            }
            return Task.CompletedTask;
        }

        public async Task HandleAsync(AutoSaveEvent message, CancellationToken cancellationToken)
        {
            await AutoSaver.Save(Tabs);
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
        private bool disposedValue;

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
                string preview = Application.Current.Args().NoPreview ? "" : $" (PREVIEW)";
#else
                string preview = "";
#endif    
                return $"DAX Studio - {Version.ToString(3)}{preview}{UserString}{AdminString}";
            }
        }
        #endregion

        public string AdminString => IsUserAdministrator() ? " [Admin]" : "";

        public bool IsUserAdministrator()
        {
            //bool value to hold our return value
            bool isAdmin;
            WindowsIdentity user = null;
            try
            {
                //get the currently logged in user
                user = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(user);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (UnauthorizedAccessException)
            {
                isAdmin = false;
            }
            catch (Exception)
            {
                isAdmin = false;
            }
            finally
            {
                user?.Dispose();
            }
            return isAdmin;
        }

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
            _eventAggregator.PublishOnUIThreadAsync(new NewDocumentEvent(Ribbon.SelectedTarget));
        }

        public void NewDocumentWithCurrentConnection()
        {
            _eventAggregator.PublishOnUIThreadAsync(new NewDocumentEvent(Ribbon.SelectedTarget,Ribbon.ActiveDocument));
        }

        public void OpenDocument()
        {
            _eventAggregator.PublishOnUIThreadAsync(new OpenFileEvent() );
        }

        public void SelectionToUpper()
        {
            _eventAggregator.PublishOnUIThreadAsync(new SelectionChangeCaseEvent(ChangeCase.ToUpper));
        }

        public void SelectionToLower()
        {
            _eventAggregator.PublishOnUIThreadAsync(new SelectionChangeCaseEvent(ChangeCase.ToLower));
        }

        public void UncommentSelection()
        {
            _eventAggregator.PublishOnUIThreadAsync(new CommentEvent(false));
        }

        public void CommentSelection()
        {
            _eventAggregator.PublishOnUIThreadAsync(new CommentEvent(true));
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
            _eventAggregator.PublishOnUIThreadAsync(new ToggleCommentEvent());
        }

        public void SelectWord()
        {
            _eventAggregator.PublishOnUIThreadAsync(new EditorHotkeyEvent( EditorHotkey.SelectWord));
        }

        public void MoveLineUp()
        {
            try
            {
                _eventAggregator.PublishOnUIThreadAsync(new EditorHotkeyEvent(EditorHotkey.MoveLineUp));
            }
            catch(Exception ex)
            {
                var msg = $"Error moving editor line up: {ex.Message}";
                Log.Error(ex, nameof(ShellViewModel), nameof(MoveLineUp), msg);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
            }
        }
        public void MoveLineDown()
        {
            try
            {
                _eventAggregator.PublishOnUIThreadAsync(new EditorHotkeyEvent(EditorHotkey.MoveLineDown));
            }
            catch (Exception ex)
            {
                var msg = $"Error moving editor line down: {ex.Message}";
                Log.Error(ex, nameof(ShellViewModel), nameof(MoveLineDown), msg);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
            }
        }
        #endregion

        #region Event Aggregator methods
        public Task HandleAsync(StartAutoSaveTimerEvent message, CancellationToken cancellationToken)
        {
            Log.Information("{class} {method} {message}", "ShellViewModel", "Handle<StartAutoSaveTimer>", "AutoSave Timer Starting");
            AutoSaveTimer.Enabled = true;
            return Task.CompletedTask;
        }

        public Task HandleAsync(StopAutoSaveTimerEvent message, CancellationToken cancellationToken)
        {
            Log.Information("{class} {method} {message}", "ShellViewModel", "Handle<StopAutoSaveTimer>", "AutoSave Timer Stopping");
            AutoSaveTimer.Enabled = false;
            return Task.CompletedTask;
        }

        public Task HandleAsync(ChangeThemeEvent message, CancellationToken cancellationToken)
        {
            ThemeManager.SetTheme(message.Theme);
            //if (message.Theme == "Dark") SetDarkTheme();
            //else SetLightTheme();
            _eventAggregator.PublishOnUIThreadAsync(new ThemeChangedEvent());
            return Task.CompletedTask;
        }


        public Task HandleAsync(UpdateHotkeys message, CancellationToken cancellationToken)
        {
            ResetInputBindings();
            return Task.CompletedTask;
        }

        public Task HandleAsync(UpdateGlobalOptions message, CancellationToken cancellationToken)
        {
            // force a refresh of the User string in case this was just turned on in the options
            DisplayName = AppTitle;
            return Task.CompletedTask;
        }


        #endregion

        // NativeWindow override to filter our WM_COPYDATA packet

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
                 
            
            // If our message
            if (msg == common.NativeMethods.WM_COPYDATA)
            {
                // msg.LParam contains a pointer to the COPYDATASTRUCT struct
                common.NativeMethods.COPYDATASTRUCT dataStruct =
                    (common.NativeMethods.COPYDATASTRUCT)Marshal.PtrToStructure(
                    lParam, typeof(common.NativeMethods.COPYDATASTRUCT));

                // Create a byte array to hold the data
                byte[] bytes = new byte[dataStruct.cbData];

                // Make a copy of the original data referenced by 
                // the COPYDATASTRUCT struct
                Marshal.Copy(dataStruct.lpData, bytes, 0,
                    dataStruct.cbData);
                // Deserialize the data back into a string
                MemoryStream stream = new MemoryStream(bytes);
                BinaryFormatter b = new BinaryFormatter();

                // This is the message sent from the other application
                string[] rawmessage = (string[])b.Deserialize(stream);

                // do something with our message
                var app = Application.Current;
                app.ReadCommandLineArgs(rawmessage);

                _host.Proxy.Port = app.Args().Port;

                if (!string.IsNullOrEmpty(app.Args().FileName))
                {
                    _eventAggregator.PublishOnUIThreadAsync(new OpenDaxFileEvent(app.Args().FileName));
                }
                else
                {
                    _eventAggregator.PublishOnUIThreadAsync(new NewDocumentEvent(null));
                }
                Application.Current.MainWindow.Activate();
                handled = true;
            }
            return IntPtr.Zero;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //  dispose managed state (managed objects)
                    _notifyIcon.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void DragEnter(IDropInfo dropInfo)
        {
            // Do nothing
        }

        public void DragOver(IDropInfo dropInfo)
        {
            //
            System.Diagnostics.Debug.WriteLine("ShellViewModel DragOver");
            if (dropInfo != null) 
            if ((dropInfo.Data is DataObject)) 
            if (((DataObject)dropInfo.Data).ContainsFileDropList())
            {
                dropInfo.Effects = DragDropEffects.Copy;
                return;
            }
            dropInfo.NotHandled = true;
        }

        public void DragLeave(IDropInfo dropInfo)
        {
            // Do Nothing
        }

        public async void Drop(IDropInfo dropInfo)
        {
            System.Diagnostics.Debug.WriteLine("ShellViewModel Drop");
            if (dropInfo == null) return;

            if (dropInfo == null
                || !(dropInfo.Data is DataObject)
                || !((DataObject)dropInfo.Data).ContainsFileDropList())
            {
                // if we are not dragging a file then mark this event as NotHandled and return
                dropInfo.NotHandled = true;
                return;
            }

            // Open the first file in the list
            var files = ((DataObject)dropInfo.Data).GetFileDropList();
            object targetEvent =
                (dropInfo.KeyStates & (DragDropKeyStates.ControlKey | DragDropKeyStates.AltKey)) == (DragDropKeyStates.ControlKey | DragDropKeyStates.AltKey)
                && Options.EnablePasteFileOnExistingWindow
                ? (object) new PasteDaxFileEvent(files[0])            
                : (object) new OpenDaxFileEvent(files[0]);
            await _eventAggregator.PublishOnUIThreadAsync(targetEvent);
            

            // TODO we should look at looping over all files, but currently this does not work,
            //      it appears that the second file starts to open before the first has finished opening 
            //      and we endup with errors or incorrectly loaded files.
        }
    }


}