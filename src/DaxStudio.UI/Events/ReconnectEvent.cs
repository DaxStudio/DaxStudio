using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class ReconnectEvent
    {
        public ReconnectEvent(string sessionId)
        {
            SessionId = sessionId;
        }
        string SessionId { get; set; }
    }
}
