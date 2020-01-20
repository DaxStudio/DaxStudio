using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [Ignore, TestClass]
    public class OneDriveHelperTests
    {
        // NOTE: this test method will only work if OneDrive is installed on the local machine
        //       (so it will not work on the AppVeyor build servers)
        [TestMethod]
        public void TestConsumerOneDrivePath()
        {
            string basePath = System.Environment.GetEnvironmentVariable("OneDriveConsumer");
            string testPath = "https://d.docs.live.net/98546e1b65a78a74/Documents/test.xlsx";
            string actual = OneDriveHelper.ConvertToLocalPath(testPath);
            
            string expected = Path.Combine(basePath, "Documents\\test.xlsx");
            Assert.AreEqual(expected, actual);
        
        }
    }
}
