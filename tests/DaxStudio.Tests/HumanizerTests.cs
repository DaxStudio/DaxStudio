using Humanizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class HumanizerTests
    {
        [TestMethod]
        public void TestDurations()
        {
            Assert.AreEqual("1 second",TimeSpan.FromMilliseconds(1000).Humanize());
            Assert.AreEqual("20 seconds", TimeSpan.FromMilliseconds(20000).Humanize());
            Assert.AreEqual("20 seconds", TimeSpan.FromMilliseconds(20300).Humanize(2));
            Assert.AreEqual("19 minutes", TimeSpan.FromMilliseconds(1184490).Humanize(2));
        }
    }
}
