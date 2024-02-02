using DaxStudio.UI.ViewModels;
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



		private string json2 = @"
{
	""expression"": ""[Customer] & \"", \"" & [Country-Region]"",
	""label"": ""customerLog"",
	""inputs"": [""'Customer'[Customer]"", ""'Customer'[Country-Region]""],
	""data"": [
		{
			""input"": [""Russell Xie"", ""United States""],
			""output"": ""Russell Xie, United States""
		},
		{
			""input"": [""Savannah Baker"", ""United States""],
			""output"": ""Savannah Baker, United States""
		},
		{
			""input"": [""Maurice Tang"", ""United States""],
			""output"": ""Maurice Tang, United States""
		},
		{
			""input"": [""Emily Wood"", ""United States""],
			""output"": ""Emily Wood, United States""
		},
		{
			""input"": [""Meghan Hernandez"", ""United States""],
			""output"": ""Meghan Hernandez, United States""
		}
	]
}
";

		private string json3 = @"
{
	""expression"": ""FILTER('Product', 'Product'[Color] = [Value])"",
	""label"": ""blah"",
	""inputs"": [""[Value]""],
	""outputs"": [""'Product'[ProductKey]"", ""'Product'[Product]"", ""'Product'[Standard Cost]"", ""'Product'[Color]"", ""'Product'[List Price]"", ""'Product'[Model]"", ""'Product'[Subcategory]"", ""'Product'[Category]"", ""'Product'[SKU]""],
	""data"": [
		{
			""input"": [""Red""],
			""rowCount"": 63,
			""output"": [
				[211, ""HL Road Frame - Red, 58"", null, ""Red"", null, ""HL Road Frame"", ""Road Frames"", ""Components"", ""FR-R92R-58""],
				[212, ""Sport-100 Helmet, Red"", 12.0278, ""Red"", 33.6442, ""Sport-100"", ""Helmets"", ""Accessories"", ""HL-U509-R""],
				[213, ""Sport-100 Helmet, Red"", 13.8782, ""Red"", 33.6442, ""Sport-100"", ""Helmets"", ""Accessories"", ""HL-U509-R""],
				[214, ""Sport-100 Helmet, Red"", 13.0863, ""Red"", 34.99, ""Sport-100"", ""Helmets"", ""Accessories"", ""HL-U509-R""],
				[238, ""HL Road Frame - Red, 62"", 747.9682, ""Red"", 1263.4598, ""HL Road Frame"", ""Road Frames"", ""Components"", ""FR-R92R-62""],
				[239, ""HL Road Frame - Red, 62"", 722.2568, ""Red"", 1301.3636, ""HL Road Frame"", ""Road Frames"", ""Components"", ""FR-R92R-62""],
				[240, ""HL Road Frame - Red, 62"", 868.6342, ""Red"", 1431.5, ""HL Road Frame"", ""Road Frames"", ""Components"", ""FR-R92R-62""],
				[241, ""HL Road Frame - Red, 44"", 747.9682, ""Red"", 1263.4598, ""HL Road Frame"", ""Road Frames"", ""Components"", ""FR-R92R-44""],
				[242, ""HL Road Frame - Red, 44"", 722.2568, ""Red"", 1301.3636, ""HL Road Frame"", ""Road Frames"", ""Components"", ""FR-R92R-44""],
				[243, ""HL Road Frame - Red, 44"", 868.6342, ""Red"", 1431.5, ""HL Road Frame"", ""Road Frames"", ""Components"", ""FR-R92R-44""]
			]
		},
		{
			""input"": [""Black""],
			""rowCount"": 129,
			""output"": [
				[210, ""HL Road Frame - Black, 58"", null, ""Black"", null, ""HL Road Frame"", ""Road Frames"", ""Components"", ""FR-R92B-58""],
				[215, ""Sport-100 Helmet, Black"", 12.0278, ""Black"", 33.6442, ""Sport-100"", ""Helmets"", ""Accessories"", ""HL-U509""],
				[216, ""Sport-100 Helmet, Black"", 13.8782, ""Black"", 33.6442, ""Sport-100"", ""Helmets"", ""Accessories"", ""HL-U509""],
				[217, ""Sport-100 Helmet, Black"", 13.0863, ""Black"", 34.99, ""Sport-100"", ""Helmets"", ""Accessories"", ""HL-U509""],
				[253, ""LL Road Frame - Black, 58"", 176.1997, ""Black"", 297.6346, ""LL Road Frame"", ""Road Frames"", ""Components"", ""FR-R38B-58""],
				[254, ""LL Road Frame - Black, 58"", 170.1428, ""Black"", 306.5636, ""LL Road Frame"", ""Road Frames"", ""Components"", ""FR-R38B-58""],
				[255, ""LL Road Frame - Black, 58"", 204.6251, ""Black"", 337.22, ""LL Road Frame"", ""Road Frames"", ""Components"", ""FR-R38B-58""],
				[256, ""LL Road Frame - Black, 60"", 176.1997, ""Black"", 297.6346, ""LL Road Frame"", ""Road Frames"", ""Components"", ""FR-R38B-60""],
				[257, ""LL Road Frame - Black, 60"", 170.1428, ""Black"", 306.5636, ""LL Road Frame"", ""Road Frames"", ""Components"", ""FR-R38B-60""],
				[258, ""LL Road Frame - Black, 60"", 204.6251, ""Black"", 337.22, ""LL Road Frame"", ""Road Frames"", ""Components"", ""FR-R38B-60""]
			]
		}
	]
}
";
		
		[TestMethod]
        public void ParseEvent_2_input_0_output_Test()
        {
			EvaluateAndLogEvent evt = new EvaluateAndLogEvent();
			
			evt.ParseJson(json);

			Assert.AreEqual("Step 1", evt.Label);
			Assert.AreEqual("AVERAGE(WorldBank[Value])", evt.Expression);
			Assert.AreEqual(13, evt.Table.Rows.Count);
        }
		[TestMethod]
		public void ParseJson_1_input_8_output_Test() {
            EvaluateAndLogEvent evt = new EvaluateAndLogEvent();

            evt.ParseJson(json3);

            Assert.AreEqual("blah", evt.Label);
            Assert.AreEqual("FILTER('Product', 'Product'[Color] = [Value])", evt.Expression);
            Assert.AreEqual(20, evt.Table.Rows.Count);
        }

		[TestMethod]
		public void ParsingJson_2_input_1_output_Test()
		{
            EvaluateAndLogEvent evt = new EvaluateAndLogEvent();

            evt.ParseJson(json2);

            Assert.AreEqual("customerLog", evt.Label);
            Assert.AreEqual("[Customer] & \", \" & [Country-Region]", evt.Expression);
            Assert.AreEqual(5, evt.Table.Rows.Count);
        }


    }
}
