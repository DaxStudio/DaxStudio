using DaxStudio.UI.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class StringExtensionTests
    {
        [TestMethod]
        public void IsFunctionKeyTest()
        {
            Assert.IsTrue( "F1".IsFunctionKey(), "F1 is a valid function key");
            Assert.IsTrue( "F12".IsFunctionKey(), "F12 is a valid function key");
            Assert.IsFalse( "M".IsFunctionKey(),"M is not a valid function key");
            Assert.IsFalse("OemComma".IsFunctionKey(), "OemComma is not a valid function key");
            Assert.IsFalse("Funny".IsFunctionKey(), "Funny is NOT a valid function key");
            Assert.IsFalse("F".IsFunctionKey(),"F is NOT a valid function key");
            Assert.IsFalse("F12a".IsFunctionKey(), "F12a is NOT a valid function key");
        }
    }
}
