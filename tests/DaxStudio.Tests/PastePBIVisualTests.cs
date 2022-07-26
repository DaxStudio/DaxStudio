using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class PastePBIVisualTests
    {
        string clipboardContent = "";
//        string clipboardContent = """
//{
//    "version": 2,
//    "sourceAction": 0,
//    "sourceSectionName": "ReportSection",
//    "visuals": [
//        {
//            "x": 21.398176291793312,
//            "y": 109.90881458966565,
//            "z": 0,
//            "width": 299.5744680851064,
//            "height": 299.5744680851064,
//            "config": {
//                "name": "ed471738735079753025",
//                "layouts": [
//                    {
//                        "id": 0,
//                        "position": {
//                            "x": 21.398176291793312,
//                            "y": 109.90881458966565,
//                            "z": 0,
//                            "width": 299.5744680851064,
//                            "height": 299.5744680851064
//                        }
//}
//                ],
//                "singleVisual": {
//    "visualType": "tableEx",
//                    "projections": {
//        "Values": [
//                            {
//            "queryRef": "DimProduct.Color"
//                            },
//                            {
//            "queryRef": "FactResellerSales.Total Reseller Sales"
//                            },
//                            {
//            "queryRef": "FactResellerSales.test.measure"
//                            }
//                        ]
//                    },
//                    "prototypeQuery": {
//        "Version": 2,
//                        "From": [
//                            {
//            "Name": "d",
//                                "Entity": "DimProduct",
//                                "Type": 0
//                            },
//                            {
//            "Name": "f",
//                                "Entity": "Fact.ResellerSales",
//                                "Type": 0
//                            }
//                        ],
//                        "Select": [
//                            {
//            "Column": {
//                "Expression": {
//                    "SourceRef": {
//                        "Source": "d"
//                                        }
//                },
//                                    "Property": "Color"
//                                },
//                                "Name": "DimProduct.Color"
//                            },
//                            {
//            "Measure": {
//                "Expression": {
//                    "SourceRef": {
//                        "Source": "f"
//                                        }
//                },
//                                    "Property": "Total Reseller Sales"
//                                },
//                                "Name": "FactResellerSales.Total Reseller Sales"
//                            },
//                            {
//            "Measure": {
//                "Expression": {
//                    "SourceRef": {
//                        "Source": "f"
//                                        }
//                },
//                                    "Property": "test.measure"
//                                },
//                                "Name": "FactResellerSales.test.measure"
//                            }
//                        ]
//                    },
//                    "drillFilterOtherVisuals": true,
//                    "filterSortOrder": 3
//                }
//            },
//            "filters": "[{\"name\":\"Filter0e6974faa0d881a2e10e\",\"expression\":{\"Column\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"DimProduct\"}},\"Property\":\"Color\"}},\"type\":\"Categorical\",\"howCreated\":0,\"isHiddenInViewMode\":false,\"ordinal\":0},{\"name\":\"Filteraccb72fd437ecb4f3af8\",\"expression\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Fact.ResellerSales\"}},\"Property\":\"test.measure\"}},\"type\":\"Advanced\",\"howCreated\":0,\"isHiddenInViewMode\":false,\"ordinal\":1},{\"name\":\"Filter8786ed0dd04f2214f888\",\"expression\":{\"Measure\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"Fact.ResellerSales\"}},\"Property\":\"Total Reseller Sales\"}},\"type\":\"Advanced\",\"howCreated\":0,\"isHiddenInViewMode\":false,\"ordinal\":2},{\"name\":\"Filterafc903baed3ea44b4ffa\",\"expression\":{\"Column\":{\"Expression\":{\"SourceRef\":{\"Entity\":\"DimProductCategory\"}},\"Property\":\"EnglishProductCategoryName\"}},\"filter\":{\"Version\":2,\"From\":[{\"Name\":\"d\",\"Entity\":\"DimProductCategory\",\"Type\":0}],\"Where\":[{\"Condition\":{\"In\":{\"Expressions\":[{\"Column\":{\"Expression\":{\"SourceRef\":{\"Source\":\"d\"}},\"Property\":\"EnglishProductCategoryName\"}}],\"Values\":[[{\"Literal\":{\"Value\":\"'Bikes'\"}}]]}}}]},\"type\":\"Categorical\",\"howCreated\":1,\"objects\":{},\"ordinal\":3}]"
//        }
//    ],
//    "referencedStaticResources": { },
//    "sessionId": "ec6297b4-1f7f-423e-953a-938399355da1"
//}
//""";

        [TestMethod]
        public void TestBasicPaste()
        {
            var jobj = JObject.Parse(clipboardContent);
            // todo - figure out how to parse clipboard content
        }
    }
}
