using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class XlsxHelperTests
    {
        [TestMethod]
        public void TestShortDateTimeFormat()
        {
            Assert.AreEqual(LargeXlsx.XlsxNumberFormat.ShortDateTime.FormatCode, XlsxHelper.GetStyle("datetime", "G").NumberFormat.FormatCode);
        }
        [TestMethod]
        public void TestLongDateFormat()
        {
            Assert.AreEqual(@"dddd,\ mmm\ dd,\ yyyy", XlsxHelper.GetStyle("datetime", "D").NumberFormat.FormatCode);
        }
        [TestMethod]
        public void TestShortDateFormat()
        {
            Assert.AreEqual(LargeXlsx.XlsxNumberFormat.ShortDate.FormatCode, XlsxHelper.GetStyle("datetime", "d").NumberFormat.FormatCode);
        }
        [TestMethod]
        public void TestLongTimeFormat()
        {
            Assert.AreEqual(@"h:mm:ss\ AM/PM", XlsxHelper.GetStyle("datetime", "T").NumberFormat.FormatCode);
        }
        [TestMethod]
        public void TestShortTimeFormat()
        {
            Assert.AreEqual("hh:mm:ss", XlsxHelper.GetStyle("datetime", "t").NumberFormat.FormatCode);
        }
        [TestMethod]
        public void TestCustomDateFormat()
        {
            Assert.AreEqual("d/mm/yyyy", XlsxHelper.GetStyle("datetime", "%d/MM/yyyy").NumberFormat.FormatCode);
        }
    }
}
