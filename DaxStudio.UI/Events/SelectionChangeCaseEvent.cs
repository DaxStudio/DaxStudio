using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.UI.Events
{
    public class SelectionChangeCaseEvent
    {
        public SelectionChangeCaseEvent(ChangeCase changeType)
        {
            ChangeType = changeType;
        }

        public ChangeCase ChangeType { get; set; }
    }

    public enum ChangeCase
    {
        ToUpper,
        ToLower
    }
}
