namespace DaxStudio.Core.Events
{
    /// <summary>
    /// Published when the user toggles the "Show Pre-Release Notifications" option. Signals that
    /// the cached version information should be cleared and a fresh version check triggered so the
    /// update indicator reflects the newly selected preview preference immediately.
    /// </summary>
    public class PreviewNotificationsChangedEvent
    {
    }
}
