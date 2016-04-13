using DaxStudio.UI.Utils.DelimiterTranslator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DelimiterTranslatorTests
    {
        /*
        [TestMethod]
        public void BasicTranslation1Test()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]), Product[Categories] = \"Bikes, Helmets\")";
            string actual = DelimiterTranslator.Translate(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]); Product[Categories] = \"Bikes, Helmets\")";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void BasicTranslation2Test()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]); Product[Prod ,;. Categories] = \"Bikes,;. Helmets\")";
            string actual = DelimiterTranslator.Translate(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]), Product[Prod ,;. Categories] = \"Bikes,;. Helmets\")";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void BasicTranslation3Test()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]); Product[Prod ,;. Rank] = 1,0)";
            string actual = DelimiterTranslator.Translate(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]), Product[Prod ,;. Rank] = 1.0)";
            Assert.AreEqual(expected, actual);
        }
        */


        [TestMethod]
        public void BasicTranslation1Test_2()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]), Product[Categories] = \"Bikes, Helmets\")";
            var dsm = new DelimiterStateMachine() ;
            string actual = dsm.ProcessString(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]); Product[Categories] = \"Bikes, Helmets\")";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void BasicTranslation2Test_2()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]); Product[Prod ,;. Categories] = \"Bikes,;. Helmets\")";
            var dsm = new DelimiterStateMachine();
            string actual = dsm.ProcessString(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]), Product[Prod ,;. Categories] = \"Bikes,;. Helmets\")";
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void BasicTranslation3Test_2()
        {
            string input = "Evaluate Filter(Values('Product'[Categories]); Product[Prod ,;. Rank] = 1,0)";
            var dsm = new DelimiterStateMachine();
            string actual = dsm.ProcessString(input);
            string expected = "Evaluate Filter(Values('Product'[Categories]), Product[Prod ,;. Rank] = 1.0)";
            Assert.AreEqual(expected, actual);
        }

    }
}
