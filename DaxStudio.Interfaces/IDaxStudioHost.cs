using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.Interfaces
{
    public interface IDaxStudioHost
    {
        IDaxStudioProxy Proxy { get; }
        bool IsExcel { get; }
    }
}
