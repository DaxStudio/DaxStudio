using System;
using System.Collections.ObjectModel;
using System.Security;
using DaxStudio.Common.Interfaces;

namespace DaxStudio.SignalR
{
    class StubGlobalOptions : IGlobalOptionsBase
    {
        public bool TraceDirectQuery
        {
            get => false;

            set => throw new NotImplementedException();
        }

    }
}
