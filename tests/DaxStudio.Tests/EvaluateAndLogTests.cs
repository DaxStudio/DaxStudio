using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class EvaluateAndLogTests
    {
		public string json = @"
{
	""expression"": ""AVERAGE(WorldBank[Value])"",
	""label"": ""Step 1"",
	""inputs"": [""'WorldBank'[TimePeriod]"", ""'Country'[Country]""],
	""data"": [
	
		{
			""input"": [2017, ""Dem. People's Rep. Korea""],
			""output"": 46.5
		},
		{
			""input"": [2018, ""Dem. People's Rep. Korea""],
			""output"": 49.5
		},
		{
			""input"": [2019, ""Dem. People's Rep. Korea""],
			""output"": 51.5
		},
		{
			""input"": [2019, ""Afghanistan""],
			""output"": 98.5
		},
		{
			""input"": [2018, ""Bangladesh""],
			""output"": 93.0
		},
		{
			""input"": [2019, ""Bangladesh""],
			""output"": 93.5
		},
		{
			""input"": [2018, ""Myanmar""],
			""output"": 73.5
		},
		{
			""input"": [2019, ""Myanmar""],
			""output"": 75.5
		},
		{
			""input"": [2018, ""Cambodia""],
			""output"": 94.5
		},
		{
			""input"": [2019, ""Cambodia""],
			""output"": 95.5
		},
		{
			""input"": [2018, ""India""],
			""output"": 96.5
		},
		{
			""input"": [2019, ""India""],
			""output"": 98.5
		},
		{
			""input"": [2019, ""Uzbekistan""],
			""output"": 100.0
		}
	]
}
";

        [TestMethod]
        public void ParseEventTest()
        {

        }
    }
}
