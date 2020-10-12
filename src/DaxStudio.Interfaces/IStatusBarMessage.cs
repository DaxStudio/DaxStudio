using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Interfaces
{
    public interface IStatusBarMessage: IDisposable
    {
        bool IsDisposed { get; }

        void Update(string v);
    }
}
