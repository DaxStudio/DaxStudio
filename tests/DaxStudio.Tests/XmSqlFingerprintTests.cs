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
    }
}
