using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;
using System.Text;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DaxDefineMeasureTests
    {
        [TestMethod]
        public void TestRegEx()
        {
            // Created this just to quickly test the RegEx (to be removed it should test the feature)
            var measureName = "Xpto";
            var measureExpression = "SUM([Sales Amount])";

            var regEx = new Regex("(?<=DEFINE)((.|\n)*?)(?=EVALUATE)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            var query = @"DEFINE
                        MEASURE 'Internet Sales'[Internet Total Sales] = SUM([Sales Amount]) 
                        MEASURE 'Internet Sales'[Internet Total Product Cost] = SUM([Total Product Cost]) 
                        MEASURE 'Internet Sales'[Internet Total Margin] = [Internet Total Sales] - [Internet Total Product Cost] 
                    EVALUATE 
                    SUMMARIZE( 
                        'Internet Sales', 
                        'Date'[Calendar Year], 
                        ""Sales"", 'Internet Sales'[Internet Total Sales], 
                        ""Cost"", 'Internet Sales'[Internet Total Product Cost], 
                        ""Margin"", 'Internet Sales'[Internet Total Margin] 
                        )
                    DEFINE
                        MEASURE 'Internet Sales'[Internet Total Sales] = SUM([Sales Amount]) 
                        MEASURE 'Internet Sales'[Internet Total Product Cost] = SUM([Total Product Cost]) 
                        MEASURE 'Internet Sales'[Internet Total Margin] = [Internet Total Sales] - [Internet Total Product Cost] 
                    EVALUATE 
                    SUMMARIZE( 
                        'Internet Sales', 
                        'Date'[Calendar Year], 
                        ""Sales"", 'Internet Sales'[Internet Total Sales], 
                        ""Cost"", 'Internet Sales'[Internet Total Product Cost], 
                        ""Margin"", 'Internet Sales'[Internet Total Margin] 
                    )
                ";            

            var measureDeclaration = string.Format("MEASURE {0} = {1}", measureName, measureExpression);

            if (regEx.IsMatch(query))
            {                
                query = regEx.Replace(query, (m) =>
                {
                    var measuresText = new StringBuilder(m.Groups[1].Value);

                    measuresText.AppendLine(measureDeclaration);

                    return measuresText.ToString();
                });
            }
            else
            {
                var queryBuilder = new StringBuilder(query);

                queryBuilder.Insert(0, string.Format("DEFINE {0}", measureDeclaration));

                query = queryBuilder.ToString();
            }
        }
    }
}
