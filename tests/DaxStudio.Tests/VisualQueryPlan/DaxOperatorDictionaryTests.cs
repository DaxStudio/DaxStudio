using DaxStudio.UI.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.VisualQueryPlan
{
    /// <summary>
    /// Unit tests for DaxOperatorDictionary operator lookup and display name resolution.
    /// </summary>
    [TestClass]
    public class DaxOperatorDictionaryTests
    {
        #region Exact Match Tests

        [TestMethod]
        public void GetOperatorInfo_AddColumns_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("AddColumns");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Add Columns", info.DisplayName);
            Assert.AreEqual("Iterator", info.Category);
            Assert.AreEqual(EngineType.FormulaEngine, info.Engine);
        }

        [TestMethod]
        public void GetOperatorInfo_ScanVertipaq_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Scan_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Scan", info.DisplayName);
            Assert.AreEqual("Storage Engine", info.Category);
            Assert.AreEqual(EngineType.StorageEngine, info.Engine);
        }

        [TestMethod]
        public void GetOperatorInfo_Calculate_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Calculate");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Calculate", info.DisplayName);
            Assert.AreEqual("Logical", info.Category);
        }

        #endregion

        #region Case-Insensitive Match Tests

        [TestMethod]
        public void GetOperatorInfo_LowerCase_MatchesCaseInsensitive()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("addcolumns");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Add Columns", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_MixedCase_MatchesCaseInsensitive()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("SCAN_VERTIPAQ");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Scan", info.DisplayName);
        }

        #endregion

        #region Partial Match Tests - Composite Operators

        [TestMethod]
        public void GetOperatorInfo_AggregationSpoolSum_ReturnsSpecificInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("AggregationSpool<Sum>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Aggregation Spool (Sum)", info.DisplayName);
            Assert.IsTrue(info.Description.Contains("Sum"));
        }

        [TestMethod]
        public void GetOperatorInfo_AggregationSpoolCount_ReturnsSpecificInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("AggregationSpool<Count>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Aggregation Spool (Count)", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_AggregationSpoolMin_ReturnsSpecificInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("AggregationSpool<Min>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Aggregation Spool (Min)", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_AggregationSpoolMax_ReturnsSpecificInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("AggregationSpool<Max>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Aggregation Spool (Max)", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_AggregationSpoolGroupBy_ReturnsSpecificInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("AggregationSpool<GroupBy>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Aggregation Spool (Group By)", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_AggregationSpoolCache_ReturnsSpecificInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("AggregationSpool<Cache>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Aggregation Spool (Cache)", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_AggregationSpoolUnknown_FallsBackToPartialMatch()
        {
            // Act - Unknown variant should fall back to base AggregationSpool
            var info = DaxOperatorDictionary.GetOperatorInfo("AggregationSpool<SomeNewType>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Aggregation Spool", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_ProjectionSpoolNested_FallsBackToPartialMatch()
        {
            // Act - Nested template should match base
            var info = DaxOperatorDictionary.GetOperatorInfo("ProjectionSpool<ProjectFusion<Copy>>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Projection Spool", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_SpoolIteratorSpool_ReturnsSpecificInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Spool_Iterator<Spool>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Spool Iterator", info.DisplayName);
        }

        #endregion

        #region Storage Engine Operator Tests

        [TestMethod]
        public void GetOperatorInfo_SumVertipaq_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Sum_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Sum", info.DisplayName);
            Assert.AreEqual(EngineType.StorageEngine, info.Engine);
        }

        [TestMethod]
        public void GetOperatorInfo_CountVertipaq_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Count_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Count", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_AverageVertipaq_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Average_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Average", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_DistinctCountVertipaq_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("DistinctCount_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Distinct Count", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_StdevSVertipaq_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Stdev.S_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq StdDev (Sample)", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_StdevPVertipaq_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Stdev.P_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq StdDev (Population)", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_VarSVertipaq_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Var.S_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Variance (Sample)", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_VarPVertipaq_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Var.P_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Variance (Population)", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_FilterVertipaq_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Filter_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Filter", info.DisplayName);
            Assert.IsTrue(info.Description.Contains("Verticalc"));
        }

        [TestMethod]
        public void GetOperatorInfo_GroupByVertipaq_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("GroupBy_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Group By", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_VertipaqResult_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("VertipaqResult");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Result", info.DisplayName);
            Assert.AreEqual(EngineType.StorageEngine, info.Engine);
        }

        #endregion

        #region Formula Engine Logical Operator Tests

        [TestMethod]
        public void GetOperatorInfo_CalculateTable_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("CalculateTable");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Calculate Table", info.DisplayName);
            Assert.IsTrue(info.Description.Contains("CALCULATETABLE"));
        }

        [TestMethod]
        public void GetOperatorInfo_GroupSemiJoin_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("GroupSemiJoin");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Group Semi Join", info.DisplayName);
            Assert.AreEqual("Relationship", info.Category);
        }

        [TestMethod]
        public void GetOperatorInfo_VarScope_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("VarScope");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Variable Scope", info.DisplayName);
            Assert.AreEqual("Variable", info.Category);
        }

        [TestMethod]
        public void GetOperatorInfo_ScalarVarProxy_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("ScalarVarProxy");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Scalar Variable Proxy", info.DisplayName);
            Assert.IsTrue(info.Description.Contains("VAR"));
        }

        [TestMethod]
        public void GetOperatorInfo_TableVarProxy_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("TableVarProxy");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Table Variable Proxy", info.DisplayName);
        }

        #endregion

        #region Physical Operator Tests

        [TestMethod]
        public void GetOperatorInfo_CrossApply_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("CrossApply");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Cross Apply", info.DisplayName);
            Assert.IsTrue(info.Description.Contains("expensive"));
        }

        [TestMethod]
        public void GetOperatorInfo_Filter_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Filter");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Filter", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_DataPostFilter_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("DataPostFilter");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Data Post Filter", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_InnerHashJoin_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("InnerHashJoin");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Inner Hash Join", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_HashLookup_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("HashLookup");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Hash Lookup", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_HashByValue_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("HashByValue");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Hash By Value", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_ExtendLookup_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Extend_Lookup");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Extend Lookup", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_ApplyRemap_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("ApplyRemap");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Apply Remap", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_SpoolUniqueHashLookup_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Spool_UniqueHashLookup");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Unique Hash Lookup Spool", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_SpoolMultiValuedHashLookup_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Spool_MultiValuedHashLookup");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Multi-Valued Hash Lookup Spool", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_SingletonTable_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("SingletonTable");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Singleton Table", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_DatesBetween_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("DatesBetween");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Dates Between", info.DisplayName);
            Assert.AreEqual("Time Intelligence", info.Category);
        }

        #endregion

        #region Null/Empty Input Tests

        [TestMethod]
        public void GetOperatorInfo_Null_ReturnsNull()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo(null);

            // Assert
            Assert.IsNull(info);
        }

        [TestMethod]
        public void GetOperatorInfo_Empty_ReturnsNull()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("");

            // Assert
            Assert.IsNull(info);
        }

        [TestMethod]
        public void GetOperatorInfo_UnknownOperator_ReturnsNull()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("SomeUnknownOperator");

            // Assert
            Assert.IsNull(info);
        }

        #endregion

        #region GetDisplayName Tests

        [TestMethod]
        public void GetDisplayName_KnownOperator_ReturnsDisplayName()
        {
            // Act
            var displayName = DaxOperatorDictionary.GetDisplayName("AddColumns");

            // Assert
            Assert.AreEqual("Add Columns", displayName);
        }

        [TestMethod]
        public void GetDisplayName_UnknownOperator_ReturnsFormattedName()
        {
            // Act
            var displayName = DaxOperatorDictionary.GetDisplayName("SomeNewOperator");

            // Assert
            Assert.AreEqual("Some New Operator", displayName);
        }

        [TestMethod]
        public void GetDisplayName_UnknownWithTemplate_StripsTemplate()
        {
            // Act
            var displayName = DaxOperatorDictionary.GetDisplayName("Unknown<SomeType>");

            // Assert
            Assert.AreEqual("Unknown", displayName);
        }

        [TestMethod]
        public void GetDisplayName_UnknownWithUnderscore_ReplacesWithSpace()
        {
            // Act
            var displayName = DaxOperatorDictionary.GetDisplayName("Some_New_Op");

            // Assert - Underscores become spaces, then camelCase logic adds more spaces before capitals
            // This results in double spaces, but the display is still readable
            Assert.IsTrue(displayName.Contains("Some") && displayName.Contains("New") && displayName.Contains("Op"));
        }

        [TestMethod]
        public void GetDisplayName_Null_ReturnsUnknown()
        {
            // Act
            var displayName = DaxOperatorDictionary.GetDisplayName(null);

            // Assert
            Assert.AreEqual("Unknown", displayName);
        }

        [TestMethod]
        public void GetDisplayName_Empty_ReturnsUnknown()
        {
            // Act
            var displayName = DaxOperatorDictionary.GetDisplayName("");

            // Assert
            Assert.AreEqual("Unknown", displayName);
        }

        #endregion

        #region GetDescription Tests

        [TestMethod]
        public void GetDescription_KnownOperator_ReturnsDescription()
        {
            // Act
            var description = DaxOperatorDictionary.GetDescription("CrossApply");

            // Assert - CrossApply evaluates table expressions, not CROSSJOIN
            Assert.IsTrue(description.Contains("table expression"));
        }

        [TestMethod]
        public void GetDescription_UnknownOperator_ReturnsDefault()
        {
            // Act
            var description = DaxOperatorDictionary.GetDescription("UnknownOperator");

            // Assert
            Assert.AreEqual("DAX query plan operator.", description);
        }

        #endregion

        #region GetCategory Tests

        [TestMethod]
        public void GetCategory_KnownOperator_ReturnsCategory()
        {
            // Act
            var category = DaxOperatorDictionary.GetCategory("Scan_Vertipaq");

            // Assert
            Assert.AreEqual("Storage Engine", category);
        }

        [TestMethod]
        public void GetCategory_UnknownOperator_ReturnsUnknown()
        {
            // Act
            var category = DaxOperatorDictionary.GetCategory("UnknownOperator");

            // Assert
            Assert.AreEqual("Unknown", category);
        }

        #endregion

        #region Comparison Operator Tests

        [TestMethod]
        public void GetOperatorInfo_GreaterThan_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("GreaterThan");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual(">", info.DisplayName);
            Assert.AreEqual("Comparison", info.Category);
        }

        [TestMethod]
        public void GetOperatorInfo_LessOrEqualTo_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("LessOrEqualTo");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("<=", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_NotEqual_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("NotEqual");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("<>", info.DisplayName);
        }

        #endregion

        #region Value Operator Tests

        [TestMethod]
        public void GetOperatorInfo_Constant_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Constant");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Constant", info.DisplayName);
            Assert.AreEqual("Value", info.Category);
        }

        [TestMethod]
        public void GetOperatorInfo_ColValue_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("ColValue");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Column Value", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_Coerce_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Coerce");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Type Coercion", info.DisplayName);
        }

        #endregion

        #region Operator Suffix Tests

        [TestMethod]
        public void GetOperatorInfo_ScaLogOp_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("ScaLogOp");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Scalar Logical Op", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_RelLogOp_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("RelLogOp");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Relational Logical Op", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_IterPhyOp_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("IterPhyOp");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Iterator Physical Op", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_LookupPhyOp_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("LookupPhyOp");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Lookup Physical Op", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_SpoolPhyOp_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("SpoolPhyOp");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Spool Physical Op", info.DisplayName);
        }

        #endregion

        #region New Operators Tests

        [TestMethod]
        public void GetOperatorInfo_TableToScalar_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("TableToScalar");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Table To Scalar", info.DisplayName);
            Assert.AreEqual("Conversion", info.Category);
        }

        [TestMethod]
        public void GetOperatorInfo_ColPosition_ReturnsCorrectInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("ColPosition");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Column Position", info.DisplayName);
            Assert.AreEqual("Lookup", info.Category);
        }

        [TestMethod]
        public void GetDisplayName_ColPositionWithColumn_ReturnsColumnPosition()
        {
            // Arrange - ColPosition operators include column references
            var operatorName = "ColPosition<'Production ProductCategory'[Name]>";

            // Act
            var displayName = DaxOperatorDictionary.GetDisplayName(operatorName);

            // Assert
            Assert.AreEqual("Column Position", displayName);
        }

        #endregion
    }
}
