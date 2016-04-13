using ADOTabular.DatabaseProfile;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DatabaseProfileTests
    {
        [TestMethod]
        public void TestAdventureWorksProfile()
        {
            var cnn = new ADOTabular.ADOTabularConnection("Data Source=localhost;Initial Catalog=AdventureWorks", ADOTabular.AdomdClientWrappers.AdomdType.AnalysisServices);
            cnn.Open();
            var db = cnn.Database;
            var profile = DatabaseProfiler.Create(db);

            Assert.AreEqual(db.Name, profile.Name);
            Assert.AreEqual(15, profile.Tables.Count);

            JsonSerializer serializer = new JsonSerializer();
            //serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Ignore;

            using (StreamWriter sw = new StreamWriter(@"d:\temp\profile.json"))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, profile);
                // {"ExpiryDate":new Date(1230375600000),"Price":0}
            }

            cnn.Close();
        }
    }
}
