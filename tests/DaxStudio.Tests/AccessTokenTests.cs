using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class AccessTokenTests
    {
        [TestMethod]
        public void HostPostfixTest()
        {
            var testCases = new Dictionary<string, Tuple<string,string>>
            {
                { "asazure://swedencentral.asazure.windows.net/contoso", new Tuple<string,string>("asazure.windows.net", "cf710c6e-dfcc-4fa8-a093-d47294e44c66") },
                { "asazure://southafricanorth.asazure.windows.net/contoso", new Tuple<string,string>("asazure.windows.net", "cf710c6e-dfcc-4fa8-a093-d47294e44c66") },
                { "powerbi://api.powerbi.com/v1.0/myorg/Contoso", new Tuple<string,string>("api.powerbi.com", "cf710c6e-dfcc-4fa8-a093-d47294e44c66") },
                { "powerbi://api.powerbi.com/v1.0/myorg/", new Tuple<string,string>("api.powerbi.com", "cf710c6e-dfcc-4fa8-a093-d47294e44c66") },
                { "powerbi://pbidedicated.usgovcloudapi.net/myorg/", new Tuple<string,string>("pbidedicated.usgovcloudapi.net", "ec3681c2-6e7d-472a-b23b-8be15bd25c15") }
            };

            foreach (var testCase in testCases)
            {
                var result = DaxStudio.Common.EntraIdHelper.GetHostPostfix(new Uri(testCase.Key));

                Assert.AreEqual(testCase.Value.Item1, result, "DomainPostFix mismatch");
                var record = DaxStudio.Common.EntraIdHelper.GetAuthenticationInformationFromDomainPostfix(result);
                Assert.AreEqual(testCase.Value.Item2, record.ApplicationId,"ApplicationId mismatch");
            }
        }
    }
}
