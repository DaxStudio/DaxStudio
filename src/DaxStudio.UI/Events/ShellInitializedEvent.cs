namespace DaxStudio.UI.Events
{
    // Published once by ShellViewModel after its view has finished loading and
    // the main window is wired up. Used as a signal for one-time UI workflows
    // (such as auto-save recovery, opening a blank document, or opening a
    // file passed on the command line) that must run only once the main
    // window's visual tree is ready to host child views.
    public class ShellInitializedEvent
    {
    }
}
