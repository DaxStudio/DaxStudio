using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.Tests
{
    /// <summary>
    /// Tests for xmSQL query fingerprinting and similarity grouping.
    /// </summary>
    [TestClass]
    public class XmSqlFingerprintTests
    {
        private XmSqlQueryGrouper _grouper;

        [TestInitialize]
        public void Setup()
        {
            _grouper = new XmSqlQueryGrouper();
        }

        // ==================== SAME STRUCTURE, DIFFERENT VALUES ====================

        [TestMethod]
        public void SameStructureDifferentFilterValues_SameFullStructuralHash()
        {
            // Two queries identical except for the WHERE filter value
            var q1 = @"SET DC_KIND=""AUTO"";
SELECT 'Product'[Color]
FROM 'Product'
WHERE 'Product'[Category] = 'Bikes';";

            var q2 = @"SET DC_KIND=""AUTO"";
SELECT 'Product'[Color]
FROM 'Product'
WHERE 'Product'[Category] = 'Clothing';";

            var fp1 = _grouper.ComputeFingerprint(q1);
            var fp2 = _grouper.ComputeFingerprint(q2);

            Assert.IsNotNull(fp1);
            Assert.IsNotNull(fp2);
            Assert.AreEqual(fp1.FullStructuralHash, fp2.FullStructuralHash,
                "Queries with same structure but different filter values should have the same structural hash");
            Assert.AreEqual(fp1.TableAccessHash, fp2.TableAccessHash,
                "Table access hash should also match");
        }

        [TestMethod]
        public void SameStructureDifferentINValues_SameHash()
        {
            var q1 = @"SELECT 'Sales'[Amount]
FROM 'Sales'
WHERE 'Sales'[Region] IN ( 'North', 'South' );";

            var q2 = @"SELECT 'Sales'[Amount]
FROM 'Sales'
WHERE 'Sales'[Region] IN ( 'East', 'West', 'Central' );";

            var fp1 = _grouper.ComputeFingerprint(q1);
            var fp2 = _grouper.ComputeFingerprint(q2);

            Assert.IsNotNull(fp1);
            Assert.IsNotNull(fp2);
            Assert.AreEqual(fp1.FullStructuralHash, fp2.FullStructuralHash);
        }

        // ==================== DIFFERENT SELECT, SAME ACCESS ====================

        [TestMethod]
        public void DifferentSelectColumns_SameTableAccessHash_DifferentStructuralHash()
        {
            var q1 = @"SELECT 'Product'[Color], 'Product'[Size]
FROM 'Product'
WHERE 'Product'[Category] = 'Bikes';";

            var q2 = @"SELECT 'Product'[Weight], 'Product'[Price]
FROM 'Product'
WHERE 'Product'[Category] = 'Bikes';";

            var fp1 = _grouper.ComputeFingerprint(q1);
            var fp2 = _grouper.ComputeFingerprint(q2);

            Assert.IsNotNull(fp1);
            Assert.IsNotNull(fp2);
            Assert.AreEqual(fp1.TableAccessHash, fp2.TableAccessHash,
                "Same FROM+WHERE should produce same table access hash");
            Assert.AreNotEqual(fp1.FullStructuralHash, fp2.FullStructuralHash,
                "Different SELECT columns should produce different full structural hash");
        }

        // ==================== DC_KIND IGNORED ====================

        [TestMethod]
        public void DifferentDcKind_SameHash()
        {
            var q1 = @"SET DC_KIND=""AUTO"";
SELECT 'Product'[Color]
FROM 'Product';";

            var q2 = @"SET DC_KIND=""DENSE"";
SELECT 'Product'[Color]
FROM 'Product';";

            var q3 = @"SET DC_KIND=""C32"";
SELECT 'Product'[Color]
FROM 'Product';";

            var fp1 = _grouper.ComputeFingerprint(q1);
            var fp2 = _grouper.ComputeFingerprint(q2);
            var fp3 = _grouper.ComputeFingerprint(q3);

            Assert.IsNotNull(fp1);
            Assert.IsNotNull(fp2);
            Assert.IsNotNull(fp3);
            Assert.AreEqual(fp1.FullStructuralHash, fp2.FullStructuralHash);
            Assert.AreEqual(fp2.FullStructuralHash, fp3.FullStructuralHash);
        }

        // ==================== NO WHERE CLAUSE ====================

        [TestMethod]
        public void QueryWithoutWhereClause_ProducesFingerprint()
        {
            var q = @"SELECT 'Product'[Color], 'Product'[Size]
FROM 'Product';";

            var fp = _grouper.ComputeFingerprint(q);

            Assert.IsNotNull(fp);
            Assert.IsNotNull(fp.FullStructuralHash);
            Assert.IsNotNull(fp.TableAccessHash);
            Assert.AreEqual("", fp.WhereColumnsSignature);
        }

        // ==================== AGGREGATION ====================

        [TestMethod]
        public void AggregationInSelect_IncludedInStructuralHash()
        {
            var q1 = @"SELECT SUM ( 'Sales'[Amount] )
FROM 'Sales'
WHERE 'Sales'[Region] = 'North';";

            var q2 = @"SELECT 'Sales'[Amount]
FROM 'Sales'
WHERE 'Sales'[Region] = 'South';";

            var fp1 = _grouper.ComputeFingerprint(q1);
            var fp2 = _grouper.ComputeFingerprint(q2);

            Assert.IsNotNull(fp1);
            Assert.IsNotNull(fp2);
            Assert.AreNotEqual(fp1.FullStructuralHash, fp2.FullStructuralHash,
                "SUM(Amount) vs plain Amount should produce different structural hashes");
            Assert.AreEqual(fp1.TableAccessHash, fp2.TableAccessHash,
                "Same FROM+WHERE should produce same table access hash");
        }

        // ==================== DEFINE TABLE (TEMP TABLES) ====================

        [TestMethod]
        public void DefineTable_ResolvesToPhysicalTable()
        {
            var q1 = @"DEFINE TABLE '$TTable2' :=
SELECT 'Time Periods'[CompKeyTP]
FROM 'Time Periods'
WHERE 'Time Periods'[Period] = 'LP';";

            var q2 = @"DEFINE TABLE '$TTable2' :=
SELECT 'Time Periods'[CompKeyTP]
FROM 'Time Periods'
WHERE 'Time Periods'[Period] = 'YTD';";

            var fp1 = _grouper.ComputeFingerprint(q1);
            var fp2 = _grouper.ComputeFingerprint(q2);

            Assert.IsNotNull(fp1);
            Assert.IsNotNull(fp2);
            Assert.AreEqual(fp1.FullStructuralHash, fp2.FullStructuralHash,
                "Same DEFINE TABLE structure with different filter values should match");
        }

        // ==================== GROUPING ====================

        [TestMethod]
        public void GroupQueries_GroupsByStructuralSimilarity()
        {
            var queries = new List<(int QueryId, string XmSql)>
            {
                (1, @"SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Bikes';"),
                (2, @"SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Clothing';"),
                (3, @"SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Accessories';"),
                (4, @"SELECT 'Product'[Size] FROM 'Product' WHERE 'Product'[Category] = 'Bikes';"),
                (5, @"SELECT SUM ( 'Sales'[Amount] ) FROM 'Sales';"),
            };

            var result = _grouper.GroupQueries(queries);

            Assert.AreEqual(5, result.TotalQueries);
            Assert.AreEqual(0, result.FailedQueries);

            // Queries 1-3 share the same full structural hash (same SELECT + same WHERE column)
            Assert.AreEqual(result.QueryToStructuralGroup[1], result.QueryToStructuralGroup[2]);
            Assert.AreEqual(result.QueryToStructuralGroup[2], result.QueryToStructuralGroup[3]);

            // Query 4 has different SELECT column, so different structural group
            Assert.AreNotEqual(result.QueryToStructuralGroup[1], result.QueryToStructuralGroup[4]);

            // But queries 1-4 share the same table access hash (same FROM + WHERE column)
            Assert.AreEqual(result.QueryToTableAccessGroup[1], result.QueryToTableAccessGroup[4]);

            // Query 5 is in its own group
            Assert.AreNotEqual(result.QueryToStructuralGroup[1], result.QueryToStructuralGroup[5]);
        }

        [TestMethod]
        public void GroupQueries_LargestGroupFirst()
        {
            var queries = new List<(int QueryId, string XmSql)>
            {
                (1, @"SELECT 'A'[X] FROM 'A' WHERE 'A'[Y] = '1';"),
                (2, @"SELECT 'A'[X] FROM 'A' WHERE 'A'[Y] = '2';"),
                (3, @"SELECT 'A'[X] FROM 'A' WHERE 'A'[Y] = '3';"),
                (4, @"SELECT 'B'[X] FROM 'B';"),
            };

            var result = _grouper.GroupQueries(queries);

            // The group with 3 members should come first
            Assert.AreEqual(3, result.StructuralGroups[0].Count);
            Assert.AreEqual(1, result.StructuralGroups[1].Count);
        }

        // ==================== DIFFERENT STRUCTURES ====================

        [TestMethod]
        public void DifferentWhereColumns_DifferentHash()
        {
            var q1 = @"SELECT 'Product'[Color]
FROM 'Product'
WHERE 'Product'[Category] = 'Bikes';";

            var q2 = @"SELECT 'Product'[Color]
FROM 'Product'
WHERE 'Product'[Brand] = 'Contoso';";

            var fp1 = _grouper.ComputeFingerprint(q1);
            var fp2 = _grouper.ComputeFingerprint(q2);

            Assert.IsNotNull(fp1);
            Assert.IsNotNull(fp2);
            Assert.AreNotEqual(fp1.FullStructuralHash, fp2.FullStructuralHash,
                "Different WHERE columns should produce different hashes");
        }

        [TestMethod]
        public void DifferentFromTable_DifferentHash()
        {
            var q1 = @"SELECT 'Product'[Color] FROM 'Product';";
            var q2 = @"SELECT 'Sales'[Color] FROM 'Sales';";

            var fp1 = _grouper.ComputeFingerprint(q1);
            var fp2 = _grouper.ComputeFingerprint(q2);

            Assert.IsNotNull(fp1);
            Assert.IsNotNull(fp2);
            Assert.AreNotEqual(fp1.FullStructuralHash, fp2.FullStructuralHash);
            Assert.AreNotEqual(fp1.TableAccessHash, fp2.TableAccessHash);
        }

        // ==================== JOIN STRUCTURES ====================

        [TestMethod]
        public void QueriesWithJoins_SameJoinStructure_SameHash()
        {
            var q1 = @"SELECT 'Sales'[Amount]
FROM 'Sales'
LEFT OUTER JOIN 'Product' ON 'Sales'[ProductKey]='Product'[ProductKey]
WHERE 'Sales'[Year] = '2024';";

            var q2 = @"SELECT 'Sales'[Amount]
FROM 'Sales'
LEFT OUTER JOIN 'Product' ON 'Sales'[ProductKey]='Product'[ProductKey]
WHERE 'Sales'[Year] = '2025';";

            var fp1 = _grouper.ComputeFingerprint(q1);
            var fp2 = _grouper.ComputeFingerprint(q2);

            Assert.IsNotNull(fp1);
            Assert.IsNotNull(fp2);
            Assert.AreEqual(fp1.FullStructuralHash, fp2.FullStructuralHash);
        }

        // ==================== SIGNATURE READABILITY ====================

        [TestMethod]
        public void Fingerprint_HasReadableSignatures()
        {
            var q = @"SELECT 'Sales'[Amount], SUM ( 'Sales'[Quantity] )
FROM 'Sales'
WHERE 'Sales'[Region] = 'North' VAND 'Sales'[Year] = '2024';";

            var fp = _grouper.ComputeFingerprint(q);

            Assert.IsNotNull(fp);
            Assert.IsTrue(fp.SelectSignature.Contains("Sales.Amount"), $"SelectSignature should contain column ref, got: {fp.SelectSignature}");
            Assert.IsTrue(fp.SelectSignature.Contains("SUM"), $"SelectSignature should contain aggregation, got: {fp.SelectSignature}");
            Assert.IsTrue(fp.FromJoinSignature.Contains("Sales"), $"FromJoinSignature should contain table name, got: {fp.FromJoinSignature}");
            Assert.IsTrue(fp.WhereColumnsSignature.Contains("Sales.Region"), $"WhereColumnsSignature should contain filter column, got: {fp.WhereColumnsSignature}");
            Assert.IsTrue(fp.WhereColumnsSignature.Contains("Sales.Year"), $"WhereColumnsSignature should contain filter column, got: {fp.WhereColumnsSignature}");
        }

        // ==================== GROUP TYPE DETERMINATION ====================

        [TestMethod]
        public void GroupType_IdenticalQueries_ReturnsIdentical()
        {
            var query = @"SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Bikes';";
            var queries = new List<(int QueryId, string XmSql)>
            {
                (1, query),
                (2, query),
                (3, query),
            };

            var result = _grouper.GroupQueries(queries);
            var queryTexts = queries.ToDictionary(q => q.QueryId, q => q.XmSql);

            // All queries are identical text, so they share a single structural group
            var groupId = result.QueryToStructuralGroup[1];
            var groupType = XmSqlQueryGrouper.DetermineGroupType(result, groupId, queryTexts);

            Assert.AreEqual("Identical queries", groupType);
        }

        [TestMethod]
        public void GroupType_IdenticalQueries_ReturnsIdentical2()
        {
            var query1 = @"SET DC_KIND=""AUTO"";
SELECT
    'Date'[Calendar Date],
    SUM ( 'Retail Sales Item'[Sold Quantity] )
FROM 'Retail Sales Item'
    LEFT OUTER JOIN 'Date'
        ON 'Retail Sales Item'[Transaction Date Sk]='Date'[Date Sk]
    LEFT OUTER JOIN 'Article'
        ON 'Retail Sales Item'[Article Typ1 Sk]='Article'[Article Typ1 Sk]
WHERE
    'Date'[Calendar Date] IN ( 45714.000000, 45690.000000, 45666.000000, 45685.000000, 45677.000000, 45661.000000, 45717.000000, 45709.000000, 45701.000000, 45669.000000..[63 total values, not all displayed] ) VAND
    'Article'[Merchandise Department Name] IN ( 'OTHER', 'HEALTH', 'BEAUTY' ) VAND
    'Retail Sales Item'[Void Transaction Line Flag] = 'N' VAND
    'Retail Sales Item'[Priceline Range Flag] = 'Y' VAND
    'Retail Sales Item'[Exclude Transaction Flag] = 'N';


Estimated size: rows = 1,645  bytes = 26,320";
            var query2 = @"SET DC_KIND=""AUTO"";
SELECT
    'Date'[Calendar Date],
    SUM ( 'Retail Sales Item'[Sold Quantity] )
FROM 'Retail Sales Item'
    LEFT OUTER JOIN 'Date'
        ON 'Retail Sales Item'[Transaction Date Sk]='Date'[Date Sk]
    LEFT OUTER JOIN 'Article'
        ON 'Retail Sales Item'[Article Typ1 Sk]='Article'[Article Typ1 Sk]
WHERE
    'Date'[Calendar Date] IN ( 45714.000000, 45690.000000, 45666.000000, 45685.000000, 45677.000000, 45661.000000, 45717.000000, 45709.000000, 45701.000000, 45669.000000..[63 total values, not all displayed] ) VAND
    'Article'[Merchandise Department Name] IN ( 'OTHER', 'HEALTH', 'BEAUTY' ) VAND
    'Retail Sales Item'[Void Transaction Line Flag] = 'N' VAND
    'Retail Sales Item'[Priceline Range Flag] = 'Y' VAND
    'Retail Sales Item'[Exclude Transaction Flag] = 'N';


Estimated size: rows = 1,645  bytes = 26,320";
            var queries = new List<(int QueryId, string XmSql)>
            {
                (1, query1),
                (2, query2),

            };

            var result = _grouper.GroupQueries(queries);
            var queryTexts = queries.ToDictionary(q => q.QueryId, q => q.XmSql);

            // All queries are identical text, so they share a single structural group
            var groupId = result.QueryToStructuralGroup[1];
            var groupType = XmSqlQueryGrouper.DetermineGroupType(result, groupId, queryTexts);

            Assert.AreEqual("Identical queries", groupType);
        }


        [TestMethod]
        public void GroupType_SameStructureDifferentFilterValues_ReturnsDifferentFilterValues()
        {
            var queries = new List<(int QueryId, string XmSql)>
            {
                (1, @"SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Bikes';"),
                (2, @"SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Clothing';"),
                (3, @"SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Accessories';"),
            };

            var result = _grouper.GroupQueries(queries);
            var queryTexts = queries.ToDictionary(q => q.QueryId, q => q.XmSql);

            var groupId = result.QueryToStructuralGroup[1];
            var groupType = XmSqlQueryGrouper.DetermineGroupType(result, groupId, queryTexts);

            Assert.AreEqual("Same structure, different filter values", groupType);
        }

        [TestMethod]
        public void GroupType_DifferentSelectColumns_ReturnsDifferentSelectColumns()
        {
            var queries = new List<(int QueryId, string XmSql)>
            {
                // Two structural groups that share the same table access (FROM+WHERE)
                // but differ in SELECT columns. Each group has multiple members with different filter values.
                (1, @"SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Bikes';"),
                (2, @"SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Clothing';"),
                (3, @"SELECT 'Product'[Size] FROM 'Product' WHERE 'Product'[Category] = 'Bikes';"),
                (4, @"SELECT 'Product'[Size] FROM 'Product' WHERE 'Product'[Category] = 'Clothing';"),
            };

            var result = _grouper.GroupQueries(queries);
            var queryTexts = queries.ToDictionary(q => q.QueryId, q => q.XmSql);

            // Queries 1&2 are in one structural group, 3&4 in another
            // Both structural groups share the same table access group
            var groupId1 = result.QueryToStructuralGroup[1];
            var groupType1 = XmSqlQueryGrouper.DetermineGroupType(result, groupId1, queryTexts);
            Assert.AreEqual("Similar structure, different SELECT columns", groupType1);

            var groupId3 = result.QueryToStructuralGroup[3];
            var groupType3 = XmSqlQueryGrouper.DetermineGroupType(result, groupId3, queryTexts);
            Assert.AreEqual("Similar structure, different SELECT columns", groupType3);
        }

        [TestMethod]
        public void GroupType_MixOfIdenticalAndDifferentValues_CorrectTypes()
        {
            var identicalQuery = @"SELECT 'Sales'[Amount] FROM 'Sales';";
            var queries = new List<(int QueryId, string XmSql)>
            {
                // Group A: identical queries
                (1, identicalQuery),
                (2, identicalQuery),
                // Group B: same structure, different filter values
                (3, @"SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Bikes';"),
                (4, @"SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Clothing';"),
            };

            var result = _grouper.GroupQueries(queries);
            var queryTexts = queries.ToDictionary(q => q.QueryId, q => q.XmSql);

            var groupIdA = result.QueryToStructuralGroup[1];
            var groupTypeA = XmSqlQueryGrouper.DetermineGroupType(result, groupIdA, queryTexts);
            Assert.AreEqual("Identical queries", groupTypeA);

            var groupIdB = result.QueryToStructuralGroup[3];
            var groupTypeB = XmSqlQueryGrouper.DetermineGroupType(result, groupIdB, queryTexts);
            Assert.AreEqual("Same structure, different filter values", groupTypeB);
        }

        [TestMethod]
        public void GroupType_SingleQueryInGroup_ReturnsIdentical()
        {
            var queries = new List<(int QueryId, string XmSql)>
            {
                (1, @"SELECT 'Product'[Color] FROM 'Product';"),
            };

            var result = _grouper.GroupQueries(queries);
            var queryTexts = queries.ToDictionary(q => q.QueryId, q => q.XmSql);

            var groupId = result.QueryToStructuralGroup[1];
            var groupType = XmSqlQueryGrouper.DetermineGroupType(result, groupId, queryTexts);

            Assert.AreEqual("Single query", groupType,
                "A single query in a group should be classified as single query");
        }

        [TestMethod]
        public void GroupType_IdenticalQueriesWithDifferentSelectSiblings_ReturnsIdentical()
        {
            // Simulates the real-world case: many structural groups share the same 
            // FROM/JOIN/WHERE (table access group), but one group has identical query text.
            // The identical group should still be classified as "Identical queries"
            // even though it has sibling structural groups with different SELECTs.
            var identicalQuery = @"SELECT 'Sales'[Amount], SUM ( 'Sales'[Qty] )
FROM 'Sales'
LEFT OUTER JOIN 'Date' ON 'Sales'[DateKey]='Date'[DateKey]
WHERE 'Date'[Year] = '2024';";

            var queries = new List<(int QueryId, string XmSql)>
            {
                // Group A: 4 identical queries (same structural group)
                (1, identicalQuery),
                (2, identicalQuery),
                (3, identicalQuery),
                (4, identicalQuery),
                // Group B: different SELECT, same FROM/JOIN/WHERE
                (5, @"SELECT 'Sales'[Region]
FROM 'Sales'
LEFT OUTER JOIN 'Date' ON 'Sales'[DateKey]='Date'[DateKey]
WHERE 'Date'[Year] = '2024';"),
                (6, @"SELECT 'Sales'[Region]
FROM 'Sales'
LEFT OUTER JOIN 'Date' ON 'Sales'[DateKey]='Date'[DateKey]
WHERE 'Date'[Year] = '2025';"),
                // Group C: yet another SELECT, same FROM/JOIN/WHERE
                (7, @"SELECT DCOUNT ( 'Sales'[TransId] )
FROM 'Sales'
LEFT OUTER JOIN 'Date' ON 'Sales'[DateKey]='Date'[DateKey]
WHERE 'Date'[Year] = '2024';"),
                (8, @"SELECT DCOUNT ( 'Sales'[TransId] )
FROM 'Sales'
LEFT OUTER JOIN 'Date' ON 'Sales'[DateKey]='Date'[DateKey]
WHERE 'Date'[Year] = '2025';"),
            };

            var result = _grouper.GroupQueries(queries);
            var queryTexts = queries.ToDictionary(q => q.QueryId, q => q.XmSql);

            // Group A (identical queries) - should be "Identical queries" despite having sibling structural groups
            var groupIdA = result.QueryToStructuralGroup[1];
            Assert.AreEqual(result.QueryToStructuralGroup[2], groupIdA, "All identical queries should be in same structural group");
            Assert.AreEqual(result.QueryToStructuralGroup[3], groupIdA);
            Assert.AreEqual(result.QueryToStructuralGroup[4], groupIdA);
            var groupTypeA = XmSqlQueryGrouper.DetermineGroupType(result, groupIdA, queryTexts);
            Assert.AreEqual("Identical queries", groupTypeA,
                "Identical queries should be classified as such even when sibling structural groups exist");

            // Group B (same structure, different filter values)
            var groupIdB = result.QueryToStructuralGroup[5];
            var groupTypeB = XmSqlQueryGrouper.DetermineGroupType(result, groupIdB, queryTexts);
            Assert.AreEqual("Similar structure, different SELECT columns", groupTypeB,
                "Non-identical group with sibling structural groups should show different SELECT columns");

            // Group C (same structure, different filter values)
            var groupIdC = result.QueryToStructuralGroup[7];
            var groupTypeC = XmSqlQueryGrouper.DetermineGroupType(result, groupIdC, queryTexts);
            Assert.AreEqual("Similar structure, different SELECT columns", groupTypeC);
        }

        [TestMethod]
        public void GroupType_EndAndCacheMatchEventsWithSameQuery_ReturnsIdentical()
        {
            // Simulates QueryEnd events (with "Estimated size:" suffix) and 
            // CacheMatch events (without suffix) for the same query.
            // They should be in the same structural group and classified as "Identical queries".
            var baseQuery = @"SET DC_KIND=""AUTO"";
SELECT 'Date'[Calendar Date], SUM ( 'Sales'[Amount] )
FROM 'Sales'
LEFT OUTER JOIN 'Date' ON 'Sales'[DateKey]='Date'[DateKey]
WHERE 'Date'[Year] IN ( '2024', '2025' );";

            var queryEndVersion = baseQuery + "\n\nEstimated size: rows = 1,645  bytes = 26,320";
            var cacheMatchVersion = baseQuery;

            var queries = new List<(int QueryId, string XmSql)>
            {
                (1, queryEndVersion),       // first execution (QueryEnd)
                (2, cacheMatchVersion),      // cache hit (CacheMatch)
                (3, queryEndVersion),        // second execution (QueryEnd)
                (4, cacheMatchVersion),      // another cache hit
            };

            var result = _grouper.GroupQueries(queries);
            var queryTexts = queries.ToDictionary(q => q.QueryId, q => q.XmSql);

            // All 4 should be in the same structural group after normalization
            var groupId = result.QueryToStructuralGroup[1];
            Assert.AreEqual(groupId, result.QueryToStructuralGroup[2],
                "End and CacheMatch events should be in the same structural group");
            Assert.AreEqual(groupId, result.QueryToStructuralGroup[3]);
            Assert.AreEqual(groupId, result.QueryToStructuralGroup[4]);

            var groupType = XmSqlQueryGrouper.DetermineGroupType(result, groupId, queryTexts);
            Assert.AreEqual("Identical queries", groupType,
                "End and CacheMatch events for the same query should be classified as identical");
        }
    }
}
