using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitComboLib.ViewModel;

namespace DaxStudio.UI.Events
{
    public class SizeUnitsUpdatedEvent
    {
        public SizeUnitsUpdatedEvent(UnitViewModel units)
        {
            Units = units;
        }

        public UnitViewModel Units { get; private set; }
    }
}
