using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    public static class Constants
    {
#if DEBUG
        public const string TestDataPath = @"..\..\..\tests\DaxStudio.Tests\data";
#else
        public const string TestDataPath = @"..\..\tests\DaxStudio.Tests\data";
#endif
    }
}
