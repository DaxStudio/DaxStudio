using DaxStudio.UI.Utils.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class AdoTabularMetadataProviderTests
    {
        [TestMethod]
        public void NullModel_ReturnsEmptyCollections()
        {
            var provider = new AdoTabularMetadataProvider(null, null);

            Assert.AreEqual(0, provider.GetTables().Count);
            Assert.AreEqual(0, provider.GetColumns("Sales").Count);
            Assert.AreEqual(0, provider.GetMeasures().Count);
            Assert.AreEqual(0, provider.GetMeasures("Sales").Count);
            Assert.AreEqual(0, provider.GetUserDefinedFunctions().Count);
            Assert.AreEqual(0, provider.GetCalendars().Count);
            Assert.AreEqual(0, provider.GetBuiltInFunctions().Count);
        }
    }
}
