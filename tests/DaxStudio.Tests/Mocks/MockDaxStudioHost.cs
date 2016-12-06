using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ADOTabular.AdomdClientWrappers;

namespace DaxStudio.Tests.Mocks
{
    class MockDaxStudioHost : IDaxStudioHost
    {
        public string CommandLineFileName
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public AdomdType ConnectionType
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsExcel
        {
            get
            {
                return false;
            }
        }

        public IDaxStudioProxy Proxy
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}
