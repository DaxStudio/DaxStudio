using DaxStudio.Tests.Assertions;
using DaxStudio.UI.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoorMansTSqlFormatterLib.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class SqlFormatterTest
    {

        [TestMethod]
        public void TestUnionAll()
        {
            string sqlQuery = @"
SELECT 
TOP (1000001) *
FROM 
(

SELECT [semijoin1].[c28],SUM([a0])
 AS [a0]
FROM 
(
(

SELECT [c15],[a0]
FROM 
(

SELECT [t1].[Date] AS [c15],[t3].[Net Price] AS [c53],[t3].[Quantity] AS [c57],
([t3].[Quantity] * [t3].[Net Price])
 AS [a0]
FROM 
((
select [$Table].[Order Number] as [Order Number],
    [$Table].[Line Number] as [Line Number],
    [$Table].[Order Date] as [Order Date],
    [$Table].[Delivery Date] as [Delivery Date],
    [$Table].[CustomerKey] as [CustomerKey],
    [$Table].[StoreKey] as [StoreKey],
    [$Table].[ProductKey] as [ProductKey],
    [$Table].[Quantity] as [Quantity],
    [$Table].[Unit Price] as [Unit Price],
    [$Table].[Net Price] as [Net Price],
    [$Table].[Unit Cost] as [Unit Cost],
    [$Table].[Currency Code] as [Currency Code],
    [$Table].[Exchange Rate] as [Exchange Rate]
from [dbo].[Sales] as [$Table]
) AS [t3]

 LEFT OUTER JOIN 

(
select [$Table].[Date] as [Date],
    [$Table].[Year] as [Year],
    [$Table].[Year Quarter] as [Year Quarter],
    [$Table].[Year Quarter Number] as [Year Quarter Number],
    [$Table].[Quarter] as [Quarter],
    [$Table].[Year Month] as [Year Month],
    [$Table].[Year Month Short] as [Year Month Short],
    [$Table].[Year Month Number] as [Year Month Number],
    [$Table].[Month] as [Month],
    [$Table].[Month Short] as [Month Short],
    [$Table].[Month Number] as [Month Number],
    [$Table].[Day of Week] as [Day of Week],
    [$Table].[Day of Week Short] as [Day of Week Short],
    [$Table].[Day of Week Number] as [Day of Week Number],
    [$Table].[Working Day] as [Working Day],
    [$Table].[Working Day Number] as [Working Day Number]
from [dbo].[Date] as [$Table]
) AS [t1] on 
(
[t3].[Order Date] = [t1].[Date]
)
)

)
 AS [t0]
)
 AS [basetable0]

 INNER JOIN 

(

(SELECT N'February 2018' AS [c28],CAST( '20180101 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180102 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180103 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180104 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180105 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180106 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180107 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180108 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180109 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180110 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180111 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180112 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180113 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180114 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180115 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180116 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180117 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180118 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180119 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180120 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180121 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180122 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180123 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180124 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180125 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180126 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180127 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180128 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180129 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180130 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180131 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180201 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180202 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180203 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180204 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180205 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180206 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180207 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180208 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180209 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180210 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180211 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180212 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180213 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180214 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180215 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180216 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180217 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180218 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180219 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180220 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180221 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180222 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180223 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180224 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180225 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180226 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180227 00:00:00' AS datetime) AS [c15] )  UNION ALL 
(SELECT N'February 2018' AS [c28],CAST( '20180228 00:00:00' AS datetime) AS [c15] ) 
)
 AS [semijoin1] on 
(

([semijoin1].[c15] = [basetable0].[c15])

)
)

GROUP BY [semijoin1].[c28]
)
 AS [MainTable]
WHERE 
(

NOT(
(
[a0] IS NULL 
)
)

)
 
";

            // SQL Formatter for DirectQuery
            ISqlTokenizer _tokenizer = new PoorMansTSqlFormatterLib.Tokenizers.TSqlStandardTokenizer();
            ISqlTokenParser _parser = new PoorMansTSqlFormatterLib.Parsers.TSqlStandardParser();
            ISqlTreeFormatter _formatter = new PoorMansTSqlFormatterLib.Formatters.TSqlDaxStudioFormatter(new PoorMansTSqlFormatterLib.Formatters.TSqlStandardFormatterOptions
            {
                BreakBeforeJoin = true,
                BreakJoinOnSections = false,
                ExpandBetweenConditions = true,
                ExpandBooleanExpressions = true,
                ExpandCaseStatements = true,
                ExpandCommaLists = true,
                ExpandInLists = true,
                HTMLColoring = false,
                IndentString = "\t", // 4 spaces to get a good indentation on tooltips
                KeywordStandardization = true,
                MaxLineWidth = 160,
                NewClauseLineBreaks = 1,
                NewStatementLineBreaks = 1,
                SpaceAfterExpandedComma = true,
                SpacesPerTab = 4,
                TrailingCommas = true,
                UppercaseKeywords = true
            });

            var tokenizedSql = _tokenizer.TokenizeSQL(sqlQuery);
            var parsedSql = _parser.ParseSQL(tokenizedSql);
            // var renamedSql = RenameSqlColumnAliases(parsedSql);
            string text = _formatter.FormatSQLTree(parsedSql);

            // No final assert.
        }

        [TestMethod]
        public void TestRemovingSessionContext()
        {
            string sqlQuery = @"EXEC sys.sp_set_session_context N'client_correlation_id'," + Environment.NewLine +
    "'EBE7BFEC-F7BD-46A3-92AC-3DD7064DA732';" + Environment.NewLine +
"SELECT TOP (1000001) 1 AS [c22]," + Environment.NewLine +  
"    [t1].[ProductID]," + Environment.NewLine +
"    [t1].[Name]," + Environment.NewLine +
"    [t1].[ProductNumber]," + Environment.NewLine +
"    [t1].[Color]," + Environment.NewLine + 
"    [t1].[StandardCost]," + Environment.NewLine +
"    [t1].[ListPrice]," + Environment.NewLine +
"    [t1].[Size]," + Environment.NewLine +
"    [t1].[Weight]," + Environment.NewLine +
"    [t1].[ProductCategoryID]," + Environment.NewLine +
"    [t1].[ProductModelID]," + Environment.NewLine +
"    [t1].[SellStartDate]," + Environment.NewLine +
"    [t1].[SellEndDate]," + Environment.NewLine +
"    [t1].[DiscontinuedDate]," + Environment.NewLine + 
"    [t1].[ThumbnailPhotoFileName]," + Environment.NewLine +
"    [t1].[rowguid]," + Environment.NewLine +
"    [t1].[ModifiedDate]" + Environment.NewLine +
"FROM (" + Environment.NewLine +
"    SELECT [ProductID]," + Environment.NewLine +   
"        [Name]," + Environment.NewLine +
"        [ProductNumber]," + Environment.NewLine +  
"        [Color]," + Environment.NewLine +
"        CAST([StandardCost] AS MONEY) AS [StandardCost]," + Environment.NewLine +
"        CAST([ListPrice] AS MONEY) AS [ListPrice]," + Environment.NewLine +
"        [Size]," + Environment.NewLine +
"        [Weight]," + Environment.NewLine +
"        [ProductCategoryID]," + Environment.NewLine +
"        [ProductModelID]," + Environment.NewLine +
"        [SellStartDate]," + Environment.NewLine +
"        [SellEndDate]," + Environment.NewLine +
"        [DiscontinuedDate]," + Environment.NewLine +
"        [ThumbnailPhotoFileName]," + Environment.NewLine +
"        [rowguid]," + Environment.NewLine +
"        [ModifiedDate]" + Environment.NewLine +
"    FROM [dbo].[Product_SM] AS [t1]" + Environment.NewLine +
"    ) AS [t1]" + Environment.NewLine +
"EXEC sys.sp_set_session_context N'client_correlation_id'," + Environment.NewLine +
"    NULL;" + Environment.NewLine 
;

            string expectedSql = @"|~K~|SELECT|~E~| |~K~|TOP|~E~| (1000001) 1 |~K~|AS|~E~| [c22]," + Environment.NewLine +
"\t[t1].[ProductID]," + Environment.NewLine +
"	[t1].[Name]," + Environment.NewLine +
"	[t1].[ProductNumber]," + Environment.NewLine +
"	[t1].[Color]," + Environment.NewLine +
"	[t1].[StandardCost]," + Environment.NewLine +
"	[t1].[ListPrice]," + Environment.NewLine +
"	[t1].[Size]," + Environment.NewLine +
"	[t1].[Weight]," + Environment.NewLine +
"	[t1].[ProductCategoryID]," + Environment.NewLine +
"	[t1].[ProductModelID]," + Environment.NewLine +
"	[t1].[SellStartDate]," + Environment.NewLine +
"	[t1].[SellEndDate]," + Environment.NewLine +
"	[t1].[DiscontinuedDate]," + Environment.NewLine +
"	[t1].[ThumbnailPhotoFileName]," + Environment.NewLine +
"	[t1].[rowguid]," + Environment.NewLine +
"	[t1].[ModifiedDate]" + Environment.NewLine +
"|~K~|FROM|~E~| (" + Environment.NewLine +
"	|~K~|SELECT|~E~| [ProductID]," + Environment.NewLine +
"		[Name]," + Environment.NewLine +
"		[ProductNumber]," + Environment.NewLine +
"		[Color]," + Environment.NewLine +
"		CAST([StandardCost] |~K~|AS|~E~| |~K~|MONEY|~E~|) |~K~|AS|~E~| [StandardCost]," + Environment.NewLine +
"		CAST([ListPrice] |~K~|AS|~E~| |~K~|MONEY|~E~|) |~K~|AS|~E~| [ListPrice]," + Environment.NewLine +
"		[Size]," + Environment.NewLine +
"		[Weight]," + Environment.NewLine +
"		[ProductCategoryID]," + Environment.NewLine +
"		[ProductModelID]," + Environment.NewLine +
"		[SellStartDate]," + Environment.NewLine +
"		[SellEndDate]," + Environment.NewLine +
"		[DiscontinuedDate]," + Environment.NewLine +
"		[ThumbnailPhotoFileName]," + Environment.NewLine +
"		[rowguid]," + Environment.NewLine +
"		[ModifiedDate]" + Environment.NewLine +
"	|~K~|FROM|~E~| [dbo].[Product_SM] |~K~|AS|~E~| [t1]" + Environment.NewLine +
"	) |~K~|AS|~E~| [t1]" + Environment.NewLine;
            // SQL Formatter for DirectQuery
            var actual = SqlFormatter.FormatSql(sqlQuery);

            StringAssertion.ShouldEqualWithDiff(expectedSql, actual, DiffStyle.Full);
        }
    }
}
