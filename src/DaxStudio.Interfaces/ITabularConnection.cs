using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Interfaces
{
    public interface ITabularConnection
    {
        void Open();
        void Ping();
        void Close();

    }
}
