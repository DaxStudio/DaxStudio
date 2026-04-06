using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    public static class Constants
    {
        public static string TestDataPath
        {
            get
            {
                // Walk up from the test assembly location to find the repo root (contains src\ and tests\)
                var dir = Path.GetDirectoryName(typeof(Constants).Assembly.Location);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir, "tests", "DaxStudio.Tests", "data");
                    if (Directory.Exists(candidate))
                        return candidate;
                    dir = Path.GetDirectoryName(dir);
                }
                throw new DirectoryNotFoundException("Could not find tests\\DaxStudio.Tests\\data from " + typeof(Constants).Assembly.Location);
            }
        }
    }
}
