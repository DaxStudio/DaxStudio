using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class UpdateTimerTextEvent
    {
        public  UpdateTimerTextEvent(string timerText)
        { TimerText = timerText; }

        public string TimerText { get; private set; }
    }
}
