using System.Data.Common;
using ADOTabular;
using ADOTabular.Utils;
using DaxStudio.Core.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    /// <summary>
    /// Verifies the cross-platform connection-string builder helper that replaced the
    /// Windows-only OleDbConnectionStringBuilder. The key requirement is that editing a
    /// connection string (adding/removing keys) round-trips correctly, preserving values
    /// including those with special characters.
    /// </summary>
    [TestClass]
    public class ConnectionStringBuilderExtensionsTests
    {
        [TestMethod]
        public void ToConnectionStringBuilder_NullOrEmpty_ReturnsEmptyBuilder()
        {
            Assert.AreEqual(0, ((string)null).ToConnectionStringBuilder().Count, "null input");
            Assert.AreEqual(0, string.Empty.ToConnectionStringBuilder().Count, "empty input");
        }

        [TestMethod]
        public void ToConnectionStringBuilder_ParsesExistingKeys()
        {
            var builder = "Data Source=localhost;Initial Catalog=Adventure Works".ToConnectionStringBuilder();
            Assert.AreEqual(2, builder.Count);
            Assert.IsTrue(builder.ContainsKey("Data Source"));
            Assert.IsTrue(builder.ContainsKey("Initial Catalog"));
        }

        [TestMethod]
        public void AddingKey_RoundTripsThroughReparse()
        {
            var builder = "Data Source=localhost".ToConnectionStringBuilder();
            builder["Initial Catalog"] = "Adventure Works";
            builder["SessionId"] = "S123";

            var reparsed = builder.ToString().ToConnectionStringBuilder();
            Assert.AreEqual("localhost", reparsed["Data Source"]);
            Assert.AreEqual("Adventure Works", reparsed["Initial Catalog"]);
            Assert.AreEqual("S123", reparsed["SessionId"]);
        }

        [TestMethod]
        public void ValuesWithSpecialCharacters_RoundTripCorrectly()
        {
            var builder = new DbConnectionStringBuilder();
            builder["Data Source"] = "powerbi://api.powerbi.com/v1.0/myorg/My Workspace";
            builder["Initial Catalog"] = "Sales; Model = 2024";   // embedded ; and =
            builder["Password"] = "p@ss\"w0rd";                     // embedded quote

            var reparsed = builder.ToString().ToConnectionStringBuilder();
            Assert.AreEqual("powerbi://api.powerbi.com/v1.0/myorg/My Workspace", reparsed["Data Source"]);
            Assert.AreEqual("Sales; Model = 2024", reparsed["Initial Catalog"]);
            Assert.AreEqual("p@ss\"w0rd", reparsed["Password"]);
        }

        [TestMethod]
        public void RemovingKey_RemovesFromOutput()
        {
            var builder = "Data Source=localhost;Roles=Test Role;EffectiveUserName=user@contoso.com".ToConnectionStringBuilder();
            builder.Remove("Roles");
            builder.Remove("EffectiveUserName");

            Assert.IsFalse(builder.ContainsKey("Roles"));
            Assert.IsFalse(builder.ContainsKey("EffectiveUserName"));
            Assert.IsTrue(builder.ContainsKey("Data Source"));
        }

        [TestMethod]
        public void GetDataSource_ReturnsValueOrEmpty()
        {
            Assert.AreEqual("localhost", "Data Source=localhost;Initial Catalog=M".ToConnectionStringBuilder().GetDataSource());
            Assert.AreEqual(string.Empty, "Initial Catalog=M".ToConnectionStringBuilder().GetDataSource(), "missing Data Source -> empty");
        }

        [TestMethod]
        public void Keys_AreCaseInsensitive()
        {
            var builder = "data source=localhost".ToConnectionStringBuilder();
            Assert.IsTrue(builder.ContainsKey("Data Source"), "lookup should be case-insensitive");
        }

        [DataTestMethod]
        [DataRow("Data Source=localhost;Roles=Reader", true)]
        [DataRow("Data Source=localhost;EffectiveUserName=user@contoso.com", true)]
        [DataRow("Data Source=localhost;Initial Catalog=Model", false)]
        public void HasRlsParameters_DetectsRlsKeys(string connectionString, bool expected)
        {
            Assert.AreEqual(expected, ADOTabularConnection.HasRlsParameters(connectionString));
        }

        [DataTestMethod]
        [DataRow("Data Source=powerbi://api.powerbi.com/v1.0/myorg/WS;Initial Catalog=M", true)]
        [DataRow("Data Source=pbidedicated://region/server;Initial Catalog=M", true)]
        [DataRow("Data Source=localhost;Initial Catalog=M", false)]
        public void IsPbiXmlaEndpoint_DetectsPowerBiEndpoints(string connectionString, bool expected)
        {
            Assert.AreEqual(expected, ConnectionManager.IsPbiXmlaEndpoint(connectionString));
        }
    }
}
