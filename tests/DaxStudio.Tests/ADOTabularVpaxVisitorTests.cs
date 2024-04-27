using ADOTabular;
using Dax.Vpax.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class ADOTabularVpaxVisitorTests
    {
        private VpaxTools.VpaxContent vpaContent;
        private ADOTabularConnection conn;

        [TestInitialize]
        public void Setup()
        {
            var testFile = $@"{Constants.TestDataPath}\AdvWorks2.vpax";
            vpaContent = Dax.Vpax.Tools.VpaxTools.ImportVpax(testFile);
            conn = new ADOTabular.ADOTabularConnection(string.Empty, ADOTabular.Enums.AdomdType.AnalysisServices);
            conn.Visitor = new MetadataVisitorVpax(conn,  vpaContent.DaxModel, vpaContent.TomDatabase);
        }

        [TestMethod]
        public void TestModelName()
        {
            Assert.AreEqual(vpaContent.DaxModel.ModelName.Name, conn.Database.Models[0].Name );
            Assert.IsFalse(conn.Database.Models[0].IsPerspective);
        }


        [TestMethod]
        public void TestTables()
        {
            Assert.AreEqual(vpaContent.DaxModel.Tables.Count, conn.Database.Models[0].Tables.Count);
        }

        [TestMethod]
        public void TestColumns()
        {
            foreach (var t in vpaContent.DaxModel.Tables)
            {
                var t2 = conn.Database.Models[0].Tables[t.TableName.Name];
                Assert.AreEqual(t.Columns.Count + t.Measures.Count, t2.Columns.Count, $"different column count for {t.TableName.Name}");
            }
        }

        [TestMethod]
        public void TestMeasures()
        {
            foreach (var t in vpaContent.DaxModel.Tables)
            {
                var t2 = conn.Database.Models[0].Tables[t.TableName.Name];
                Assert.AreEqual(t.Measures.Count, t2.Measures.Count, $"different column count for {t.TableName.Name}");
            }
        }
    }
}
