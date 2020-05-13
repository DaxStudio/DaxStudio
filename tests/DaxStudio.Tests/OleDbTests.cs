using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DaxStudio.Tests
{
    [TestClass,Ignore]
    public class OleDbTests
    {
        [TestMethod]
        public void ConnectToPowerBIUsingOleDb()
        {
            var connStr = "Provider=MSOLAP.7;Data Source=https://analysis.windows.net/powerbi/api;;Initial Catalog=09f08a95-3837-47a3-8ac8-8d70fef2418f;Location=https://wabi-us-north-central-redirect.analysis.windows.net/xmla?vs=sobe_wowvirtualserver&db=09f08a95-3837-47a3-8ac8-8d70fef2418f;MDX Compatibility= 1; MDX Missing Member Mode= Error; Safety Options= 2; Update Isolation Level= 2";
            //connStr = "Provider=MSOLAP.7;Data Source=Localhost;Initial Catalog=AdventureWorks";
            var conn = new System.Data.OleDb.OleDbConnection(connStr);
            
            try
            {
                conn.Open();
                Assert.IsTrue(conn.State == System.Data.ConnectionState.Open);
                conn.Close();
            }
            catch (Exception ex)
            {
                Assert.Fail("Exception Opening Connection: {0}", ex.Message);
            }
        }

        //[TestMethod]
        //public void ConnectToPowerBIUsingAdomd()
        //{
        //    var connStr = "Integrated Security=ClaimsToken;Data Source=https://analysis.windows.net/powerbi/api;;Initial Catalog=09f08a95-3837-47a3-8ac8-8d70fef2418f;Location=https://wabi-us-north-central-redirect.analysis.windows.net/xmla?vs=sobe_wowvirtualserver&db=09f08a95-3837-47a3-8ac8-8d70fef2418f;MDX Compatibility= 1; MDX Missing Member Mode= Error; Safety Options= 2; Update Isolation Level= 2";
        //    connStr = "Provider=MSOLAP.7;Data Source=Localhost;Initial Catalog=AdventureWorks";
        //    var conn = new Microsoft.AnalysisServices.AdomdClient.AdomdConnection(connStr);

        //    try
        //    {
        //        conn.Open();
        //        Assert.IsTrue(conn.State == System.Data.ConnectionState.Open);
        //        conn.Close();
        //    }
        //    catch (Exception ex)
        //    {
        //        Assert.Fail("Exception Opening Connection: {0}", ex.Message);
        //    }
        //}
    }
}
