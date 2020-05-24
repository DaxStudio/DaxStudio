using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Interfaces
{
    public interface IZoomable
    {
        double Scale { get; set; }
        event EventHandler OnScaleChanged;
    }
}
