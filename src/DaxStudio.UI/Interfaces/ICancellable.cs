using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Interfaces
{
    internal interface ICancellable
    {
        public bool IsCancelled { get; }
    }
}
