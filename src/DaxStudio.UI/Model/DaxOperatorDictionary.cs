using System.Collections.Generic;

namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Information about a DAX query plan operator.
    /// </summary>
    public class DaxOperatorInfo
    {
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public EngineType Engine { get; set; }
        /// <summary>
        /// Optional dax.guide URL for the corresponding DAX function (if applicable).
        /// </summary>
        public string DaxGuideUrl { get; set; }
    }

    /// <summary>
    /// Provides human-readable names and explanations for DAX query plan operators.
    /// Based on SQLBI DAX Query Plans documentation.
    /// </summary>
    public static class DaxOperatorDictionary
    {
        private static readonly Dictionary<string, DaxOperatorInfo> _operators = new Dictionary<string, DaxOperatorInfo>(System.StringComparer.OrdinalIgnoreCase)
        {
            // Iteration Operators (Physical Plan)
            ["AddColumns"] = new DaxOperatorInfo
            {
                DisplayName = "Add Columns",
                Description = "Adds calculated columns to each row of an input table. Iterates over rows and evaluates expressions for each.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/addcolumns/"
            },
            ["SingletonTable"] = new DaxOperatorInfo
            {
                DisplayName = "Singleton Table",
                Description = "Creates a single-row table, typically used as the starting point for scalar calculations.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/row/"
            },
            ["CrossApply"] = new DaxOperatorInfo
            {
                DisplayName = "Cross Apply",
                Description = "Evaluates a table expression for each row of an outer input. Can be expensive with large tables.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine
                // Note: CrossApply is a query engine operator, not the CROSSJOIN DAX function
            },
            ["Filter"] = new DaxOperatorInfo
            {
                DisplayName = "Filter",
                Description = "Filters rows from an input table based on a condition. Only rows satisfying the predicate are passed through.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/filter/"
            },
            ["DataPostFilter"] = new DaxOperatorInfo
            {
                DisplayName = "Data Post Filter",
                Description = "Filters data after retrieval from storage. Applied after initial scan results are returned.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine
            },
            ["Cache"] = new DaxOperatorInfo
            {
                DisplayName = "Cache",
                Description = "Storage Engine datacache - temporary uncompressed storage for SE query results that the Formula Engine reads. Represents a request made to the Storage Engine.",
                Category = "Cache",
                Engine = EngineType.StorageEngine
            },

            // Spool Operators
            ["Spool_Iterator"] = new DaxOperatorInfo
            {
                DisplayName = "Spool Iterator",
                Description = "Iterates over cached/spooled data. Reads from a previously computed result set stored in memory.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["Spool_Iterator<Spool>"] = new DaxOperatorInfo
            {
                DisplayName = "Spool Iterator",
                Description = "Iterates over spooled/materialized data. Reads from cached result set.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["SpoolLookup"] = new DaxOperatorInfo
            {
                DisplayName = "Spool Lookup",
                Description = "Looks up values in a spooled (cached) result set using key columns. Efficient for repeated lookups.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["ProjectionSpool"] = new DaxOperatorInfo
            {
                DisplayName = "Projection Spool",
                Description = "Stores projected columns from a table scan for later use. Reduces redundant data retrieval.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["AggregationSpool"] = new DaxOperatorInfo
            {
                DisplayName = "Aggregation Spool",
                Description = "Caches aggregated results for reuse. Stores pre-computed aggregations to avoid recalculation.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["AggregationSpool<Cache>"] = new DaxOperatorInfo
            {
                DisplayName = "Aggregation Spool (Cache)",
                Description = "Cached aggregation spool. Stores aggregation results for repeated access.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["AggregationSpool<Sum>"] = new DaxOperatorInfo
            {
                DisplayName = "Aggregation Spool (Sum)",
                Description = "Sum aggregation spool. Stores running sum results for efficient access.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["AggregationSpool<Count>"] = new DaxOperatorInfo
            {
                DisplayName = "Aggregation Spool (Count)",
                Description = "Count aggregation spool. Stores count results for efficient access.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["AggregationSpool<Min>"] = new DaxOperatorInfo
            {
                DisplayName = "Aggregation Spool (Min)",
                Description = "Min aggregation spool. Stores minimum value results for efficient access.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["AggregationSpool<Max>"] = new DaxOperatorInfo
            {
                DisplayName = "Aggregation Spool (Max)",
                Description = "Max aggregation spool. Stores maximum value results for efficient access.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["AggregationSpool<GroupBy>"] = new DaxOperatorInfo
            {
                DisplayName = "Aggregation Spool (Group By)",
                Description = "Group by aggregation spool. Stores grouped aggregation results.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["Spool_UniqueHashLookup"] = new DaxOperatorInfo
            {
                DisplayName = "Unique Hash Lookup Spool",
                Description = "Performs unique hash-based lookup from spooled data. Efficient for single-value lookups.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["Spool_MultiValuedHashLookup"] = new DaxOperatorInfo
            {
                DisplayName = "Multi-Valued Hash Lookup Spool",
                Description = "Performs multi-valued hash-based lookup from spooled data. Returns multiple matching rows.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },

            // Hash and Join Operators
            ["InnerHashJoin"] = new DaxOperatorInfo
            {
                DisplayName = "Inner Hash Join",
                Description = "Performs an inner join using hash algorithm. Efficient for joining large datasets.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine
            },
            ["HashLookup"] = new DaxOperatorInfo
            {
                DisplayName = "Hash Lookup",
                Description = "Hash-based value lookup operation. Fast O(1) lookup using hash table.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine
            },
            ["HashByValue"] = new DaxOperatorInfo
            {
                DisplayName = "Hash By Value",
                Description = "Spools data organized by hash value for efficient lookup operations.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["Extend_Lookup"] = new DaxOperatorInfo
            {
                DisplayName = "Extend Lookup",
                Description = "Extends rows with additional lookup values from related data.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine
            },
            ["ApplyRemap"] = new DaxOperatorInfo
            {
                DisplayName = "Apply Remap",
                Description = "Applies column remapping to transform column references.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine
            },

            // Storage Engine Operators
            ["Scan_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Scan",
                Description = "Scans data from the in-memory VertiPaq storage engine. This is a Storage Engine operation - fast columnar scan.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine
            },
            ["Sum_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Sum",
                Description = "Performs SUM aggregation directly in the VertiPaq storage engine. Highly optimized for columnar data.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine,
                DaxGuideUrl = "https://dax.guide/sum/"
            },
            ["Count_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Count",
                Description = "Performs COUNT aggregation directly in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine,
                DaxGuideUrl = "https://dax.guide/count/"
            },
            ["Min_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Min",
                Description = "Performs MIN aggregation directly in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine,
                DaxGuideUrl = "https://dax.guide/min/"
            },
            ["Max_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Max",
                Description = "Performs MAX aggregation directly in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine,
                DaxGuideUrl = "https://dax.guide/max/"
            },
            ["Average_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Average",
                Description = "Performs AVERAGE aggregation directly in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine,
                DaxGuideUrl = "https://dax.guide/average/"
            },
            ["DistinctCount_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Distinct Count",
                Description = "Performs DISTINCTCOUNT aggregation directly in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine,
                DaxGuideUrl = "https://dax.guide/distinctcount/"
            },
            ["Stdev.S_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq StdDev (Sample)",
                Description = "Performs sample standard deviation aggregation in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine,
                DaxGuideUrl = "https://dax.guide/stdev.s/"
            },
            ["Stdev.P_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq StdDev (Population)",
                Description = "Performs population standard deviation aggregation in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine,
                DaxGuideUrl = "https://dax.guide/stdev.p/"
            },
            ["Var.S_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Variance (Sample)",
                Description = "Performs sample variance aggregation in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine,
                DaxGuideUrl = "https://dax.guide/var.s/"
            },
            ["Var.P_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Variance (Population)",
                Description = "Performs population variance aggregation in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine,
                DaxGuideUrl = "https://dax.guide/var.p/"
            },
            ["Filter_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Filter",
                Description = "Applies Verticalc predicates (complex filter expressions) in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine
            },
            ["GroupBy_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Group By",
                Description = "Modifies column names and incorporates rollup columns in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine
            },
            ["VertipaqResult"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Result",
                Description = "Returns results from a VertiPaq storage engine query. Data is passed back to the Formula Engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine
            },

            // DirectQuery Operators
            ["DirectQueryResult"] = new DaxOperatorInfo
            {
                DisplayName = "DirectQuery Result",
                Description = "Returns results from a DirectQuery operation. Query is sent to the external data source (SQL Server, etc.) rather than VertiPaq.",
                Category = "DirectQuery",
                Engine = EngineType.DirectQuery
            },

            // Logical Operators
            ["Calculate"] = new DaxOperatorInfo
            {
                DisplayName = "Calculate",
                Description = "Evaluates an expression in a modified filter context. Core DAX operation for context transition.",
                Category = "Logical",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/calculate/"
            },
            ["CalculateTable"] = new DaxOperatorInfo
            {
                DisplayName = "Calculate Table",
                Description = "Evaluates a table expression in a modified filter context (CALCULATETABLE).",
                Category = "Logical",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/calculatetable/"
            },
            ["GroupSemiJoin"] = new DaxOperatorInfo
            {
                DisplayName = "Group Semi Join",
                Description = "Join that returns matched rows from the primary table. Used for relationship traversal.",
                Category = "Relationship",
                Engine = EngineType.FormulaEngine
            },
            ["VarScope"] = new DaxOperatorInfo
            {
                DisplayName = "Variable Scope",
                Description = "Container for variable definitions. Holds VAR declarations for use in expressions.",
                Category = "Variable",
                Engine = EngineType.FormulaEngine
            },
            ["ScalarVarProxy"] = new DaxOperatorInfo
            {
                DisplayName = "Scalar Variable Proxy",
                Description = "Returns the value of a scalar variable from a VarScope. Used when referencing VAR in expressions.",
                Category = "Variable",
                Engine = EngineType.FormulaEngine
            },
            ["TableVarProxy"] = new DaxOperatorInfo
            {
                DisplayName = "Table Variable Proxy",
                Description = "Returns the value of a table variable from a VarScope. Used when referencing table VAR in expressions.",
                Category = "Variable",
                Engine = EngineType.FormulaEngine
            },
            ["Proxy"] = new DaxOperatorInfo
            {
                DisplayName = "Variable Proxy",
                Description = "Physical iterator that references a variable. Provides access to variable values during query execution.",
                Category = "Variable",
                Engine = EngineType.FormulaEngine
            },
            ["SumX"] = new DaxOperatorInfo
            {
                DisplayName = "Sum X (Iterator)",
                Description = "Iterates over a table and sums the result of an expression evaluated for each row. Row-by-row aggregation.",
                Category = "Aggregation",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/sumx/"
            },
            ["CountRows"] = new DaxOperatorInfo
            {
                DisplayName = "Count Rows",
                Description = "Counts the number of rows in a table.",
                Category = "Aggregation",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/countrows/"
            },
            ["DependOnCols"] = new DaxOperatorInfo
            {
                DisplayName = "Column Dependency",
                Description = "Indicates which columns the operation depends on. Used for query optimization.",
                Category = "Metadata",
                Engine = EngineType.Unknown
            },
            ["RequiredCols"] = new DaxOperatorInfo
            {
                DisplayName = "Required Columns",
                Description = "Specifies columns required by the operation. Used for query optimization and data retrieval.",
                Category = "Metadata",
                Engine = EngineType.Unknown
            },

            // Time Intelligence
            ["PreviousQuarter"] = new DaxOperatorInfo
            {
                DisplayName = "Previous Quarter",
                Description = "Time intelligence function that shifts the filter context to the previous quarter.",
                Category = "Time Intelligence",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/previousquarter/"
            },
            ["PreviousMonth"] = new DaxOperatorInfo
            {
                DisplayName = "Previous Month",
                Description = "Time intelligence function that shifts the filter context to the previous month.",
                Category = "Time Intelligence",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/previousmonth/"
            },
            ["PreviousYear"] = new DaxOperatorInfo
            {
                DisplayName = "Previous Year",
                Description = "Time intelligence function that shifts the filter context to the previous year.",
                Category = "Time Intelligence",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/previousyear/"
            },
            ["SamePeriodLastYear"] = new DaxOperatorInfo
            {
                DisplayName = "Same Period Last Year",
                Description = "Time intelligence function that returns the same period in the previous year.",
                Category = "Time Intelligence",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/sameperiodlastyear/"
            },
            ["DatesBetween"] = new DaxOperatorInfo
            {
                DisplayName = "Dates Between",
                Description = "Returns a table of dates between two specified dates.",
                Category = "Time Intelligence",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/datesbetween/"
            },

            // Join and Relationship Operators
            ["LookupValue"] = new DaxOperatorInfo
            {
                DisplayName = "Lookup Value",
                Description = "Retrieves a value from a related table based on matching criteria. Similar to VLOOKUP.",
                Category = "Lookup",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/lookupvalue/"
            },
            ["RelatedTable"] = new DaxOperatorInfo
            {
                DisplayName = "Related Table",
                Description = "Returns a table of related rows from the many side of a relationship.",
                Category = "Relationship",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/relatedtable/"
            },
            ["Related"] = new DaxOperatorInfo
            {
                DisplayName = "Related",
                Description = "Returns a related value from the one side of a relationship.",
                Category = "Relationship",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/related/"
            },

            // Physical Operator Types
            ["IterPhyOp"] = new DaxOperatorInfo
            {
                DisplayName = "Iterator Physical Op",
                Description = "Physical operator that iterates over data. Processes rows one at a time.",
                Category = "Physical",
                Engine = EngineType.FormulaEngine
            },
            ["LookupPhyOp"] = new DaxOperatorInfo
            {
                DisplayName = "Lookup Physical Op",
                Description = "Physical operator that performs key-based lookups into cached data.",
                Category = "Physical",
                Engine = EngineType.FormulaEngine
            },
            ["SpoolPhyOp"] = new DaxOperatorInfo
            {
                DisplayName = "Spool Physical Op",
                Description = "Physical operator that stores intermediate results in memory for reuse.",
                Category = "Physical",
                Engine = EngineType.FormulaEngine
            },
            ["ScaLogOp"] = new DaxOperatorInfo
            {
                DisplayName = "Scalar Logical Op",
                Description = "Logical operator that produces a scalar (single) value.",
                Category = "Logical",
                Engine = EngineType.FormulaEngine
            },
            ["RelLogOp"] = new DaxOperatorInfo
            {
                DisplayName = "Relational Logical Op",
                Description = "Logical operator that produces a table (relational) result.",
                Category = "Logical",
                Engine = EngineType.FormulaEngine
            },

            // Other Common Operators
            ["Union"] = new DaxOperatorInfo
            {
                DisplayName = "Union",
                Description = "Combines multiple tables into one, appending rows.",
                Category = "Set",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/union/"
            },
            ["Except"] = new DaxOperatorInfo
            {
                DisplayName = "Except",
                Description = "Returns rows from the first table that don't exist in the second table.",
                Category = "Set",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/except/"
            },
            ["Intersect"] = new DaxOperatorInfo
            {
                DisplayName = "Intersect",
                Description = "Returns rows that exist in both tables.",
                Category = "Set",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/intersect/"
            },
            ["Distinct"] = new DaxOperatorInfo
            {
                DisplayName = "Distinct",
                Description = "Returns unique rows from a table, removing duplicates.",
                Category = "Set",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/distinct/"
            },
            ["Values"] = new DaxOperatorInfo
            {
                DisplayName = "Values",
                Description = "Returns distinct values from a column, including blank if present in the data.",
                Category = "Set",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/values/"
            },
            ["All"] = new DaxOperatorInfo
            {
                DisplayName = "All",
                Description = "Removes filters from a table or column. Returns all rows ignoring any filters.",
                Category = "Filter Modifier",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/all/"
            },
            ["AllSelected"] = new DaxOperatorInfo
            {
                DisplayName = "All Selected",
                Description = "Removes filters from a table while keeping external filters from slicers/visuals.",
                Category = "Filter Modifier",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/allselected/"
            },
            ["TopN"] = new DaxOperatorInfo
            {
                DisplayName = "Top N",
                Description = "Returns the top N rows from a table based on a specified expression.",
                Category = "Filter",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/topn/"
            },
            ["Order"] = new DaxOperatorInfo
            {
                DisplayName = "Order",
                Description = "Sorts the result set by specified columns. Typically the root operator for queries returning sorted results.",
                Category = "Sort",
                Engine = EngineType.FormulaEngine
            },
            ["Query"] = new DaxOperatorInfo
            {
                DisplayName = "Query",
                Description = "Synthetic root node containing all top-level query components (DEFINE variables and EVALUATE).",
                Category = "Root",
                Engine = EngineType.FormulaEngine
            },
            ["GroupBy"] = new DaxOperatorInfo
            {
                DisplayName = "Group By",
                Description = "Groups rows by specified columns and allows aggregation calculations.",
                Category = "Aggregation",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/groupby/"
            },
            ["Summarize"] = new DaxOperatorInfo
            {
                DisplayName = "Summarize",
                Description = "Creates a summary table grouped by specified columns with optional aggregations.",
                Category = "Aggregation",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/summarize/"
            },

            // Copy and Data Movement
            ["Copy"] = new DaxOperatorInfo
            {
                DisplayName = "Copy",
                Description = "Copies data from one location to another within the query execution.",
                Category = "Data Movement",
                Engine = EngineType.FormulaEngine
            },
            ["ProjectFusion"] = new DaxOperatorInfo
            {
                DisplayName = "Project Fusion",
                Description = "Optimizes projection operations by fusing multiple projections together.",
                Category = "Optimization",
                Engine = EngineType.FormulaEngine
            },

            // Comparison Operators (can be collapsed for simpler display)
            ["GreaterThan"] = new DaxOperatorInfo
            {
                DisplayName = ">",
                Description = "Compares if the left operand is greater than the right operand.",
                Category = "Comparison",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/op/greater-than/"
            },
            ["GreaterOrEqualTo"] = new DaxOperatorInfo
            {
                DisplayName = ">=",
                Description = "Compares if the left operand is greater than or equal to the right operand.",
                Category = "Comparison",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/op/greater-than-or-equal-to/"
            },
            ["LessThan"] = new DaxOperatorInfo
            {
                DisplayName = "<",
                Description = "Compares if the left operand is less than the right operand.",
                Category = "Comparison",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/op/less-than/"
            },
            ["LessOrEqualTo"] = new DaxOperatorInfo
            {
                DisplayName = "<=",
                Description = "Compares if the left operand is less than or equal to the right operand.",
                Category = "Comparison",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/op/less-than-or-equal-to/"
            },
            ["Equal"] = new DaxOperatorInfo
            {
                DisplayName = "=",
                Description = "Compares if the left operand equals the right operand.",
                Category = "Comparison",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/op/equal-to/"
            },
            ["NotEqual"] = new DaxOperatorInfo
            {
                DisplayName = "<>",
                Description = "Compares if the left operand does not equal the right operand.",
                Category = "Comparison",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/op/not-equal-to/"
            },

            // Value/Reference Operators (typically children of comparisons)
            ["Constant"] = new DaxOperatorInfo
            {
                DisplayName = "Constant",
                Description = "A constant literal value used in an expression.",
                Category = "Value",
                Engine = EngineType.FormulaEngine
            },
            ["ColValue"] = new DaxOperatorInfo
            {
                DisplayName = "Column Value",
                Description = "References the value of a column in the current row context.",
                Category = "Value",
                Engine = EngineType.FormulaEngine
            },
            ["Coerce"] = new DaxOperatorInfo
            {
                DisplayName = "Type Coercion",
                Description = "Converts a value from one data type to another.",
                Category = "Value",
                Engine = EngineType.FormulaEngine
            },
            ["Variant->Numeric/Date"] = new DaxOperatorInfo
            {
                DisplayName = "Variant to Numeric/Date",
                Description = "Coerces a Variant type (from conditional expressions returning different types) to Numeric or Date.",
                Category = "Type Coercion",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/dt/variant/"
            },
            ["Variant->String"] = new DaxOperatorInfo
            {
                DisplayName = "Variant to String",
                Description = "Coerces a Variant type (from conditional expressions returning different types) to String.",
                Category = "Type Coercion",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/dt/variant/"
            },
            ["Median"] = new DaxOperatorInfo
            {
                DisplayName = "Median",
                Description = "Calculates the median value (50th percentile) of a column or expression.",
                Category = "Aggregation",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/median/"
            },
            ["Multiply"] = new DaxOperatorInfo
            {
                DisplayName = "Multiply",
                Description = "Multiplies two values together. Used for arithmetic calculations like quantity × price.",
                Category = "Arithmetic",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/op/multiplication/"
            },
            ["Subtract"] = new DaxOperatorInfo
            {
                DisplayName = "Subtract",
                Description = "Subtracts the right operand from the left operand.",
                Category = "Arithmetic",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/op/subtraction/"
            },
            ["Add"] = new DaxOperatorInfo
            {
                DisplayName = "Add",
                Description = "Adds two values together.",
                Category = "Arithmetic",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/op/addition/"
            },
            ["Divide"] = new DaxOperatorInfo
            {
                DisplayName = "Divide",
                Description = "Divides the left operand by the right operand.",
                Category = "Arithmetic",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/op/division/"
            },

            // Table Constructor
            ["TableCtor"] = new DaxOperatorInfo
            {
                DisplayName = "Table Constructor",
                Description = "Builds an inline table using curly brace syntax { }. Single column tables use 'Value' as column name; multi-column tables use Value1, Value2, etc.",
                Category = "Table",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/op/table-constructor/"
            },

            // DirectQuery and Partitioning Operators
            ["PartitionIntoGroups"] = new DaxOperatorInfo
            {
                DisplayName = "Partition Into Groups",
                Description = "Partitions data into groups for parallel processing or batched operations. Shows #Groups and #Rows properties.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine
            },
            ["AggregationSpool<Order>"] = new DaxOperatorInfo
            {
                DisplayName = "Aggregation Spool (Order)",
                Description = "Aggregation spool for ORDER BY operations. Stores sorted results for efficient access.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["AggregationSpool<Top>"] = new DaxOperatorInfo
            {
                DisplayName = "Aggregation Spool (Top)",
                Description = "Aggregation spool for TOP N operations. Stores top N results for efficient access.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["AggregationSpool<Last>"] = new DaxOperatorInfo
            {
                DisplayName = "Aggregation Spool (Last)",
                Description = "Aggregation spool storing the last value. Used with time intelligence functions.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["AggregationSpool<TableToScalar>"] = new DaxOperatorInfo
            {
                DisplayName = "Aggregation Spool (Table to Scalar)",
                Description = "Aggregation spool that converts table results to scalar values.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["CalculateTable_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Calculate Table",
                Description = "Evaluates CALCULATETABLE in the VertiPaq storage engine with modified filter context.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine,
                DaxGuideUrl = "https://dax.guide/calculatetable/"
            },
            ["TreatAs"] = new DaxOperatorInfo
            {
                DisplayName = "Treat As",
                Description = "Applies virtual relationships by treating column values as if they were in another table. Used for dynamic filtering without physical relationships.",
                Category = "Filter Modifier",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/treatas/"
            },

            // Logical/Boolean Operators
            ["Not"] = new DaxOperatorInfo
            {
                DisplayName = "Not",
                Description = "Logical NOT operator. Inverts a boolean value.",
                Category = "Logical",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/not/"
            },
            ["ISBLANK"] = new DaxOperatorInfo
            {
                DisplayName = "Is Blank",
                Description = "Checks if a value is blank. Returns TRUE if blank, FALSE otherwise.",
                Category = "Logical",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/isblank/"
            },
            ["First"] = new DaxOperatorInfo
            {
                DisplayName = "First",
                Description = "Returns the first value in a column or expression. Used in time intelligence and aggregation contexts.",
                Category = "Aggregation",
                Engine = EngineType.FormulaEngine
                // Note: Different from FIRST() visual calculation function
            },

            // Time Intelligence Operators
            ["StartOfYear"] = new DaxOperatorInfo
            {
                DisplayName = "Start of Year",
                Description = "Returns the start of the year for a given date. Used in YTD calculations.",
                Category = "Time Intelligence",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/startofyear/"
            },
            ["LastDate"] = new DaxOperatorInfo
            {
                DisplayName = "Last Date",
                Description = "Returns the last date in a date column within the current filter context.",
                Category = "Time Intelligence",
                Engine = EngineType.FormulaEngine,
                DaxGuideUrl = "https://dax.guide/lastdate/"
            },
            ["TableToScalar"] = new DaxOperatorInfo
            {
                DisplayName = "Table To Scalar",
                Description = "Converts a single-row, single-column table to a scalar value. Used internally for time intelligence functions.",
                Category = "Conversion",
                Engine = EngineType.FormulaEngine
            },
            ["ColPosition"] = new DaxOperatorInfo
            {
                DisplayName = "Column Position",
                Description = "Returns the ordinal position of a value within a column. Used for sorting and ranking operations.",
                Category = "Lookup",
                Engine = EngineType.FormulaEngine
            }
        };

        /// <summary>
        /// Gets information about a DAX operator by its name.
        /// </summary>
        /// <param name="operatorName">The operator name (e.g., "AddColumns", "Scan_Vertipaq")</param>
        /// <returns>Operator info if found, null otherwise</returns>
        public static DaxOperatorInfo GetOperatorInfo(string operatorName)
        {
            if (string.IsNullOrEmpty(operatorName))
                return null;

            // Dictionary uses OrdinalIgnoreCase comparer, so lookup is case-insensitive
            if (_operators.TryGetValue(operatorName, out var info))
                return info;

            // Try partial match for composite operators like "ProjectionSpool<ProjectFusion<Copy>>"
            foreach (var kvp in _operators)
            {
                if (operatorName.StartsWith(kvp.Key, System.StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return null;
        }

        /// <summary>
        /// Gets a human-readable display name for an operator.
        /// </summary>
        /// <param name="operatorName">The raw operator name</param>
        /// <returns>Human-readable name or the original name if not found</returns>
        public static string GetDisplayName(string operatorName)
        {
            // Special handling for ColValue and ColPosition - return just the operator name
            // The column reference is displayed separately via DisplayDetail in PlanNodeViewModel
            if (operatorName != null)
            {
                if (operatorName.StartsWith("ColValue<", System.StringComparison.Ordinal) ||
                    operatorName.StartsWith("ColValue", System.StringComparison.Ordinal))
                {
                    return "Column Value";
                }
                if (operatorName.StartsWith("ColPosition<", System.StringComparison.Ordinal) ||
                    operatorName.StartsWith("ColPosition", System.StringComparison.Ordinal))
                {
                    return "Column Position";
                }
            }

            var info = GetOperatorInfo(operatorName);
            return info?.DisplayName ?? FormatUnknownOperator(operatorName);
        }

        /// <summary>
        /// Gets a description for an operator.
        /// </summary>
        /// <param name="operatorName">The raw operator name</param>
        /// <returns>Description or a generic message if not found</returns>
        public static string GetDescription(string operatorName)
        {
            var info = GetOperatorInfo(operatorName);
            return info?.Description ?? "DAX query plan operator.";
        }

        /// <summary>
        /// Gets the category for an operator.
        /// </summary>
        /// <param name="operatorName">The raw operator name</param>
        /// <returns>Category name or "Unknown" if not found</returns>
        public static string GetCategory(string operatorName)
        {
            var info = GetOperatorInfo(operatorName);
            return info?.Category ?? "Unknown";
        }

        /// <summary>
        /// Gets the dax.guide URL for an operator, if applicable.
        /// </summary>
        /// <param name="operatorName">The raw operator name</param>
        /// <returns>dax.guide URL or null if not available</returns>
        public static string GetDaxGuideUrl(string operatorName)
        {
            var info = GetOperatorInfo(operatorName);
            return info?.DaxGuideUrl;
        }

        /// <summary>
        /// Formats an unknown operator name into a more readable form.
        /// </summary>
        private static string FormatUnknownOperator(string operatorName)
        {
            if (string.IsNullOrEmpty(operatorName))
                return "Unknown";

            // Remove template parameters like <ProjectFusion<Copy>>
            var angleBracketIndex = operatorName.IndexOf('<');
            if (angleBracketIndex > 0)
                operatorName = operatorName.Substring(0, angleBracketIndex);

            // Convert underscores to spaces
            operatorName = operatorName.Replace("_", " ");

            // Add spaces before capitals (camelCase to Title Case)
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < operatorName.Length; i++)
            {
                if (i > 0 && char.IsUpper(operatorName[i]) && !char.IsUpper(operatorName[i - 1]))
                    result.Append(' ');
                result.Append(operatorName[i]);
            }

            return result.ToString();
        }
    }
}
