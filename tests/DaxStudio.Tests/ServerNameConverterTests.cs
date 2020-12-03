using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class ServerNameConverterTests
    {
        [TestMethod]
        public void TestPowerBiXmlaConvert()
        {
            var conv = new DaxStudio.UI.Converters.ServerNameConverter();
            var name = "powerbi://api.powerbi.com/v1.0/myorg/Xmla Test";
            string expectedName = "powerbi://.../myorg/Xmla Test";
            string actualName = (string)conv.Convert(name, typeof(string), null, CultureInfo.CurrentCulture);

            Assert.AreEqual( expectedName, actualName);
        }

        [TestMethod]
        public void TestPowerBiDedicatedXmlaConvert()
        {
            var conv = new DaxStudio.UI.Converters.ServerNameConverter();
            var name = "pbidedicated://api.powerbi.com/v1.0/myorg/Xmla Test";
            string expectedName = "pbidedicated://.../myorg/Xmla Test";
            string actualName = (string)conv.Convert(name, typeof(string), null, CultureInfo.CurrentCulture);

            Assert.AreEqual(expectedName, actualName);
        }

        [TestMethod]
        public void TestPowerBiAzureXmlaConvert()
        {
            var conv = new DaxStudio.UI.Converters.ServerNameConverter();
            var name = "pbiazure://api.powerbi.com/v1.0/myorg/Xmla Test";
            string expectedName = "pbiazure://.../myorg/Xmla Test";
            string actualName = (string)conv.Convert(name, typeof(string), null, CultureInfo.CurrentCulture);

            Assert.AreEqual(expectedName, actualName);
        }

        [TestMethod]
        public void TestPowerBiUnknownPrefixDoesNotConvert()
        {
            var conv = new DaxStudio.UI.Converters.ServerNameConverter();
            var name = "pbiblah://api.powerbi.com/v1.0/myorg/Xmla Test";
            string expectedName = "pbiblah://.../myorg/Xmla Test";
            string actualName = (string)conv.Convert(name, typeof(string), null, CultureInfo.CurrentCulture);

            Assert.AreNotEqual(expectedName, actualName);
        }

        [TestMethod]
        public void TestAsAzureConvert()
        {
            var conv = new DaxStudio.UI.Converters.ServerNameConverter();
            var name = "asazure://australiasoutheast.asazure.windows.net/dev";
            string expectedName = "asazure://.../dev";
            string actualName = (string)conv.Convert(name, typeof(string), null, CultureInfo.CurrentCulture);

            Assert.AreEqual(expectedName, actualName);
        }

        [TestMethod]
        public void TestStandardNameConvert()
        {
            var conv = new DaxStudio.UI.Converters.ServerNameConverter();
            var name = "localhost:1234";
            string expectedName = "localhost:1234";
            string actualName = (string)conv.Convert(name, typeof(string), null, CultureInfo.CurrentCulture);

            Assert.AreEqual(expectedName, actualName);
        }
    }
}
