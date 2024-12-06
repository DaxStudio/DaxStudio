using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class BuildTests
    {
        [TestMethod]
        public void SystemRuntimeComplierServicesUnsafeCheck()
        {
            var msg = "There is a hard coded AssemblyBinding in the standalone app.config and a reference in DAXStudio.UI to v5.0.0.0 to fix the static excel export issues #891";
            System.Reflection.Assembly assembly = System.Reflection.Assembly.Load("System.Runtime.CompilerServices.Unsafe");
            Assert.AreEqual(new Version(6, 0, 1, 0), assembly.GetName().Version, msg);
        }

        [TestMethod]
        public void SystemMemoryCheck()
        {
            var msg = "Added a reference in dscmd, daxstudio.exe and the test assemblies to v4.6.0.0 to fix the static excel export issues #891";
            System.Reflection.Assembly assembly = System.Reflection.Assembly.Load("System.Memory");
            Assert.AreEqual(new Version(4, 0, 2, 0), assembly.GetName().Version, msg);
        }
    }
}
