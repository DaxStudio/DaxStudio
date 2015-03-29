using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class NavigateToLocationEvent
    {
        public NavigateToLocationEvent(int row, int column)
        { 
            Row= row;
            Column = column;
        }

        public int Row { get;private set; }
        public int Column { get; private set; }
    }
}
