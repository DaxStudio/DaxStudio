using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class ServerTimingDetailsUpdatedEvent
    {
        public ServerTimingDetailsUpdatedEvent(ServerTimingDetailsViewModel serverTimingDetails)
        {
            ServerTimingDetails = serverTimingDetails;
        }
        public ServerTimingDetailsViewModel ServerTimingDetails { get; set; }
    }
}
