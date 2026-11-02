using Caliburn.Micro;
using DaxStudio.Core.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace DaxStudio.Tests
{
    [TestClass]
    public class ConnectionManagerDependencyTests
    {
        private static ConnectionManager NewConnectionManager()
        {
            return new ConnectionManager(Substitute.For<IEventAggregator>());
        }

        [TestMethod]
        public void GetQueryDependencyTablesReturnsEmptyForNullQuery()
        {
            var cnn = NewConnectionManager();
            var tables = cnn.GetQueryDependencyTables(null);
            Assert.IsNotNull(tables);
            Assert.AreEqual(0, tables.Count);
        }

        [TestMethod]
        public void GetQueryDependencyTablesReturnsEmptyForBlankQuery()
        {
            var cnn = NewConnectionManager();
            var tables = cnn.GetQueryDependencyTables("   ");
            Assert.IsNotNull(tables);
            Assert.AreEqual(0, tables.Count);
        }
    }
}
