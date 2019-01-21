using DaxStudio.UI.Model;
using System.Collections.Generic;

namespace DaxStudio.UI.Events
{
    public class AutoSaveRecoveryEvent
    {
        public AutoSaveRecoveryEvent(Dictionary<int,AutoSaveIndex> autoSaveMasterIndex)
        {
            AutoSaveMasterIndex = autoSaveMasterIndex;
            
        }
        public Dictionary<int,AutoSaveIndex> AutoSaveMasterIndex { get; private set; }
        public bool RecoveryInProgress { get; set; }
    }
}
