using System;

namespace DaxStudio.Common.Interfaces
{
    public interface IStatusBarMessage: IDisposable
    {
        bool IsDisposed { get; }

        void Update(string v);
    }
}
