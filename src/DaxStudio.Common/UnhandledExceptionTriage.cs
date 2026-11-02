using System;
using System.Runtime.InteropServices;

namespace DaxStudio.Common
{
    /// <summary>
    /// The action a host should take in response to an unhandled dispatcher exception.
    /// </summary>
    public enum UnhandledExceptionAction
    {
        /// <summary>The exception is not recognised - the host should log a crash report and shut down.</summary>
        Fatal,
        /// <summary>The exception is known to be transient - mark it handled and keep running.</summary>
        Recover,
        /// <summary>
        /// The WPF render thread has died. This is <b>not</b> recoverable: WPF "zombies" the
        /// composition partition and never reconnects it, so nothing will ever paint again.
        /// The host must terminate (or, when hosted inside another process, tell the user to
        /// restart) rather than swallow the exception and leave a frozen window behind.
        /// </summary>
        FatalRenderThreadFailure
    }

    /// <summary>
    /// The outcome of triaging an unhandled dispatcher exception.
    /// </summary>
    public class UnhandledExceptionDecision
    {
        public UnhandledExceptionDecision(UnhandledExceptionAction action, string logMessage, string userMessage)
        {
            Action = action;
            LogMessage = logMessage;
            UserMessage = userMessage;
        }

        public UnhandledExceptionAction Action { get; }

        /// <summary>Short description used for the warning written to the log file.</summary>
        public string LogMessage { get; }

        /// <summary>Message to show the user, or <c>null</c> when the failure should be swallowed silently.</summary>
        public string UserMessage { get; }

        /// <summary>True only when the process can carry on running normally.</summary>
        public bool IsRecoverable => Action == UnhandledExceptionAction.Recover;
    }

    /// <summary>
    /// Triage for unhandled WPF dispatcher exceptions in the standalone application.
    /// <para>
    /// Kept separate from <c>EntryPoint</c> so the classification rules can be unit tested
    /// without standing up a WPF <c>Application</c>. Note this is only relevant to
    /// <c>DaxStudio.Standalone</c> - the Excel add-in has no WPF UI of its own (it hosts an
    /// OWIN/SignalR server and launches daxstudio.exe as a separate process), so it can never
    /// raise these failures.
    /// </para>
    /// </summary>
    public class UnhandledExceptionTriage
    {
        // WPF MIL (Media Integration Layer) composition errors, raised when the WPF render
        // thread hits a fatal error - typically a GPU driver bug/TDR, a display mode change or
        // resource exhaustion.
        //
        // IMPORTANT: these are NOT recoverable. When the render thread fails, WPF "zombies" the
        // composition partition (see MediaContext.NotifyPartitionIsZombie, which simply throws)
        // and there is no reconnect path anywhere in WPF - MediaSystem.ConnectTransport is only
        // ever called once, from MediaSystem.Startup. Once this fires, nothing in the process
        // will ever render again, so swallowing the exception just leaves the user with a frozen
        // window and no crash report. See
        // https://learn.microsoft.com/troubleshoot/developer/dotnet/framework/general/wpf-render-thread-failures
        public const int UCEERR_DISPLAYSTATEINVALID = unchecked((int)0x88980403);
        public const int UCEERR_NOTIFICATIONSDROPPED = unchecked((int)0x88980404);
        public const int UCEERR_RENDERTHREADFAILURE = unchecked((int)0x88980406);

        public const int CLIPBRD_E_BAD_DATA = unchecked((int)0x800401D3);
        public const int CLIPBRD_E_CANT_OPEN = unchecked((int)0x800401D0);
        public const int RPC_E_WRONG_THREAD = unchecked((int)0x8001010E);

        /// <summary>
        /// Message WPF uses for the MediaContext.NotifyPartitionIsZombie flavour of render
        /// thread failure (SR.MediaContext_RenderThreadError).
        /// </summary>
        public const string RenderThreadErrorMessage = "An unspecified error occurred on the render thread.";

        private static readonly UnhandledExceptionTriage _default = new UnhandledExceptionTriage();

        /// <summary>Shared instance used by the standalone application.</summary>
        public static UnhandledExceptionTriage Default => _default;

        public static bool IsRenderThreadFailure(int hresult)
        {
            return hresult == UCEERR_RENDERTHREADFAILURE
                || hresult == UCEERR_DISPLAYSTATEINVALID
                || hresult == UCEERR_NOTIFICATIONSDROPPED;
        }

        /// <summary>
        /// Classifies an unhandled dispatcher exception. Returns <c>null</c> when the exception
        /// is not one this helper knows about, leaving the caller to apply its own handling.
        /// </summary>
        public UnhandledExceptionDecision Triage(Exception exception)
        {
            if (exception == null) return null;

            if (exception is COMException comException)
            {
                var hresult = comException.ErrorCode;

                if (IsRenderThreadFailure(hresult))
                {
                    return new UnhandledExceptionDecision(UnhandledExceptionAction.FatalRenderThreadFailure,
                        $"WPF render-thread failure 0x{hresult:X8} - the composition partition is zombied and cannot be recovered",
                        null);
                }

                switch (hresult)
                {
                    case CLIPBRD_E_BAD_DATA:
                        return new UnhandledExceptionDecision(UnhandledExceptionAction.Recover,
                            "COM Error while accessing clipboard: CLIPBRD_E_BAD_DATA",
                            "CLIPBRD_E_BAD_DATA Error - Clipboard operation failed, please try again");
                    case CLIPBRD_E_CANT_OPEN:
                        return new UnhandledExceptionDecision(UnhandledExceptionAction.Recover,
                            "COM Error while accessing clipboard: CLIPBRD_E_CANT_OPEN",
                            "CLIPBRD_E_CANT_OPEN Error - Clipboard operation failed, please try again");
                    case RPC_E_WRONG_THREAD:
                        return new UnhandledExceptionDecision(UnhandledExceptionAction.Recover,
                            "COM Error while accessing clipboard: RPC_E_WRONG_THREAD",
                            "RPC_E_WRONG_THREAD Error - Clipboard operation failed, please try again");
                }

                if (exception.Message == "A drag operation is already in progress")
                {
                    return new UnhandledExceptionDecision(UnhandledExceptionAction.Recover,
                        "COM Error while doing DragDrop: " + exception.Message,
                        exception.Message + "\nPlease retry the operation");
                }

                return null;
            }

            // The other face of the same failure - the render thread zombied the partition and
            // MediaContext.NotifyPartitionIsZombie mapped it to an InvalidOperationException
            // rather than surfacing the raw HRESULT through SyncFlush.
            if (exception is InvalidOperationException
                && exception.Message == RenderThreadErrorMessage)
            {
                return new UnhandledExceptionDecision(UnhandledExceptionAction.FatalRenderThreadFailure,
                    "WPF render-thread failure (NotifyPartitionIsZombie) - the composition partition is zombied and cannot be recovered",
                    null);
            }

            // Known WPF framework bug - deleting the temporary cursor file fails because of
            // permissions or because another process has it open. Purely cosmetic.
            if (exception is UnauthorizedAccessException
                && exception.StackTrace != null
                && exception.StackTrace.Contains("GridViewColumnHeader.GetCursor"))
            {
                return new UnhandledExceptionDecision(UnhandledExceptionAction.Recover,
                    "WPF GridViewColumnHeader cursor temp file access denied (non-fatal)", null);
            }

            // Known Caliburn.Micro race - the conductor's async Closing continuation reaches
            // Window.Close() after WPF has already begun closing the window. The window is
            // closing anyway so the exception is meaningless.
            if (exception is InvalidOperationException
                && exception.StackTrace != null
                && exception.StackTrace.Contains("Caliburn.Micro.WindowConductor")
                && (exception.Message?.Contains("while a Window is closing") ?? false))
            {
                return new UnhandledExceptionDecision(UnhandledExceptionAction.Recover,
                    "Caliburn.Micro WindowConductor close race (non-fatal)", null);
            }

            return null;
        }
    }
}
