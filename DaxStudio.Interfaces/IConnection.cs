using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Interfaces
{
    public interface IConnection
    {
        int Spid { get; }
        bool IsPowerPivot { get; }
        bool IsConnected { get;  }
        string SelectedDatabase { get; }
        string ServerName { get; }
    }
}
