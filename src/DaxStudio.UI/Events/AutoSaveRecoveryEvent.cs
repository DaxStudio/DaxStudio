using DaxStudio.UI.Model;

namespace DaxStudio.UI.Events
{
    public class AutoSaveRecoveryEvent
    {
        public AutoSaveRecoveryEvent(AutoSaveIndex autoSaveIndex)
        {
            AutoSaveIndex = autoSaveIndex;
            
        }
        public AutoSaveIndex AutoSaveIndex { get; private set; }
        public bool RecoveryInProgress { get; set; }
    }
}
