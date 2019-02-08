using ADOTabular;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.AnalysisServices;
using DaxStudio.UI.Extensions;

namespace DaxStudio.UI.Utils
{


    public static class ModelAnalyzer
    {
        public static DataTable RecoverColumnExpressions(ADOTabularConnection connection)
        {
            /*
             *  [TABLE] AS [Table], 
                [OBJECT] as [Column], 
                TRIM ( [EXPRESSION] ) as [Expression],
                '' AS [Description]
             */
            var result = new DataTable("ColumnExpressions");
            result.Columns.Add("Table", typeof(string));
            result.Columns.Add("Column", typeof(string));
            result.Columns.Add("Expression", typeof(string));
            result.Columns.Add("Description", typeof(string));

            foreach (var tbl in connection.Database.Models[0].Tables)
            {
                foreach (var col in tbl.Columns)
                {
                    if (col.ObjectType == ADOTabularObjectType.Column && col.MeasureExpression.Length > 0)
                    {
                        var row = result.NewRow();
                        row["Table"] = tbl.Caption;
                        row["Column"] = col.Caption;
                        row["Expression"] = col.MeasureExpression;
                        row["Description"] = "";
                        result.Rows.Add(row);
                    }
                }
            }
            return result;
        }

        private class ModelAnalyzerRelationship
        {
            /// <summary>
            /// Relationship for compatibility level 1100 (Tabular/TOM)
            /// </summary>
            /// <param name="relationship"></param>
            public ModelAnalyzerRelationship(SingleColumnRelationship relationship)
            {
                IsActive = relationship.IsActive;
                Name = relationship.Name;
                CrossFilteringBehavior = relationship.CrossFilteringBehavior.ToString();
                FromTable = relationship.FromTable.Name;
                FromColumn = relationship.FromColumn.Name;
                FromCardinality = relationship.FromCardinality.ToString();
                ToTable = relationship.ToTable.Name;
                ToColumn = relationship.ToColumn.Name;
                ToCardinality = relationship.ToCardinality.ToString();

            }

            /// <summary>
            /// Relationship for compatibility level 1100 (Multidimensional/XML)
            /// </summary>
            /// <param name="relationship"></param>
            public ModelAnalyzerRelationship(Microsoft.AnalysisServices.Relationship relationship) {
                IsActive = (relationship.ActiveState == ActiveState.ActiveStateActive);
                Name = relationship.ID;
                CrossFilteringBehavior = relationship.CrossFilterDirection.ToString();
                FromTable = relationship.FromRelationshipEnd.DimensionID;
                FromColumn = null; // TODO - we don't know this for compatibility level 1100
                FromCardinality = null; // TODO - we don't know this for compatibility level 1100
                ToTable = relationship.ToRelationshipEnd.DimensionID;
                ToColumn = null; // TODO - we don't know this for compatibility level 1100
                ToCardinality = null; // TODO - we don't know this for compatibility level 1100

            }
            public string FromCardinality { get; private set; }
            public string FromColumn { get; private set; }
            public string FromTable { get; private set; }
            public bool IsActive { get; private set; }
            public string Name { get; private set; }
            public string ToCardinality { get; private set; }
            public string ToColumn { get; private set; }
            public string ToTable { get; private set; }
            public string CrossFilteringBehavior { get; private set; }
        }
        private class ModelAnalyzerTable
        {
            public string TableName;
            public string Query;
            public int MinCompatibilityLevel = 1100;
            /// <summary>
            /// ignore the query in case it is no longer supported in a compatibility level
            /// usually there is a replacement with another query depending on the 
            /// compatibility level, applying a proper range for different queries
            /// However, it is possible to create multiple versions of the same table 
            /// with different compatibility levels (to enable support with different and older reports)
            /// </summary>
            public int MaxCompatibilityLevel = int.MaxValue;
            public Func<ADOTabularConnection, DataTable> RecoverFunction;
        }


        private static List<ModelAnalyzerTable> GetQueries() {
            return new List<ModelAnalyzerTable>
            {
                new ModelAnalyzerTable()
                {
                    TableName = "Tables",
                    MinCompatibilityLevel = 1100,
                    Query = @"
SELECT 
    DIMENSION_NAME AS TABLE_NAME, 
    TABLE_ID AS TABLE_ID,
    ROWS_COUNT AS ROWS_IN_TABLE
FROM  $SYSTEM.DISCOVER_STORAGE_TABLES
WHERE RIGHT ( LEFT ( TABLE_ID, 2 ), 1 ) <> '$'
ORDER BY DIMENSION_NAME
"
                },
                new ModelAnalyzerTable()
                {
                    TableName = "ColumnsCardinality",
                    MinCompatibilityLevel = 1100,
                    Query = @"
SELECT
    DIMENSION_NAME AS TABLE_NAME, 
    TABLE_ID AS COLUMN_HIERARCHY_ID,
    ROWS_COUNT - 3 AS COLUMN_CARDINALITY
FROM $SYSTEM.DISCOVER_STORAGE_TABLES
WHERE LEFT ( TABLE_ID, 2 ) = 'H$'
ORDER BY TABLE_ID
"
                },
                new ModelAnalyzerTable()
                {
                    TableName = "Columns",
                    MinCompatibilityLevel = 1100,
                    Query = @"
SELECT
    DIMENSION_NAME AS TABLE_NAME, 
    COLUMN_ID AS COLUMN_ID, 
    ATTRIBUTE_NAME AS COLUMN_NAME, 
    DATATYPE AS [Data Type],
    DICTIONARY_SIZE AS DICTIONARY_SIZE_BYTES,
    COLUMN_ENCODING AS COLUMN_ENCODING_INT
FROM  $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMNS
WHERE COLUMN_TYPE = 'BASIC_DATA'
ORDER BY [DIMENSION_NAME] + '_' + [ATTRIBUTE_NAME] ASC
"
                },
                new ModelAnalyzerTable()
                {
                    TableName = "ColumnsSegments",
                    Query = @"
SELECT 
    DIMENSION_NAME AS TABLE_NAME, 
    PARTITION_NAME, 
    COLUMN_ID AS COLUMN_NAME, 
    SEGMENT_NUMBER, 
    TABLE_PARTITION_NUMBER, 
    RECORDS_COUNT AS SEGMENT_ROWS,
    USED_SIZE,
    COMPRESSION_TYPE,
    BITS_COUNT,
    BOOKMARK_BITS_COUNT,
    VERTIPAQ_STATE
FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS
WHERE RIGHT ( LEFT ( TABLE_ID, 2 ), 1 ) <> '$'
ORDER BY [DIMENSION_NAME] + '_' + [COLUMN_ID] ASC"
                },
                new ModelAnalyzerTable()
                {
                    TableName = "ColumnsHierarchies",
                    Query = @"
SELECT 
    DIMENSION_NAME AS TABLE_NAME, 
    COLUMN_ID AS STRUCTURE_NAME,
    SEGMENT_NUMBER, 
    TABLE_PARTITION_NUMBER, 
    USED_SIZE,
    TABLE_ID AS COLUMN_HIERARCHY_ID
FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS
WHERE LEFT ( TABLE_ID, 2 ) = 'H$'
ORDER BY [DIMENSION_NAME] + '_' + [COLUMN_ID] ASC
"
                },
                new ModelAnalyzerTable()
                {
                    TableName = "UserHierarchies",
                    Query = @"
SELECT 
    DIMENSION_NAME AS TABLE_NAME, 
    COLUMN_ID AS STRUCTURE_NAME,
    USED_SIZE,
    TABLE_ID AS HIERARCHY_ID
FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS
WHERE LEFT ( TABLE_ID, 2 ) = 'U$'
ORDER BY [DIMENSION_NAME] + '_' + [COLUMN_ID] ASC
"
                },
                new ModelAnalyzerTable()
                {
                    TableName = "RelationshipsSize",
                    Query = @"
SELECT 
    DIMENSION_NAME AS TABLE_NAME, 
    USED_SIZE,
    TABLE_ID AS RELATIONSHIP_ID
FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS
WHERE LEFT ( TABLE_ID, 2 ) = 'R$'
ORDER BY [DIMENSION_NAME] + '_' + [TABLE_ID] ASC
"
                },
                new ModelAnalyzerTable()
                {
                     TableName = "MeasuresExpressions",
                     MinCompatibilityLevel = 1100,
                     Query = @"
SELECT DISTINCT 
    '' as [TableID],
    [MEASUREGROUP_NAME] as [Table], 
    [MEASURE_NAME] as [Measure], 
    TRIM ( [Expression] ) as [Expression], 
    [DESCRIPTION] as [Description],
    NOT [MEASURE_IS_VISIBLE] as [IsHidden],
	DATA_TYPE AS [DataType],
	MEASURE_DISPLAY_FOLDER as [DisplayFolder],
	DEFAULT_FORMAT_STRING as [FormatString]
FROM $SYSTEM.MDSCHEMA_MEASURES 
WHERE MEASURE_NAME <> '__XL_Count of Models'
AND MEASURE_NAME <> '__Default measure'
AND TRIM ( [Expression] ) <> '' 
AND TRIM ( [MEASUREGROUP_NAME] ) <> ''
ORDER BY [MEASUREGROUP_NAME] + '_' + [MEASURE_NAME] ASC
"
                },
                new ModelAnalyzerTable()
                {
                     TableName = "MeasuresExpressions",
                     MinCompatibilityLevel = 1200,
                     Query = @"
SELECT
    [TableID],
    null as [Table],
    [Name] AS [Measure],
    TRIM ( [Expression] ) as [Expression], 
    [Description],
    [IsHidden],
    [State],
    [DataType],
    [DisplayFolder],
    [FormatString]
FROM $SYSTEM.TMSCHEMA_MEASURES
ORDER BY [Name] ASC
"
                 },
                new ModelAnalyzerTable()
                 {
                     TableName = "ColumnsExpressions",
                     Query = @"
SELECT DISTINCT  
    [TABLE] AS [Table], 
    [OBJECT] as [Column], 
    TRIM ( [EXPRESSION] ) as [Expression],
    '' AS [Description]
FROM $SYSTEM.DISCOVER_CALC_DEPENDENCY  
WHERE OBJECT_TYPE = 'CALC_COLUMN'  
ORDER BY [TABLE] + '_' + [OBJECT] ASC
"
                    ,RecoverFunction = ModelAnalyzer.RecoverColumnExpressions
                 },


        new ModelAnalyzerTable()
                 {
                     TableName = "ColumnsMetadata",
                     MinCompatibilityLevel = 1200,
                     Query = @"
SELECT    
    [TableID],    
    null as [Table],
    [ID] AS [ColumnID],    
    [ExplicitName],
    [InferredName],
    TRIM ( [Expression] ) AS [Expression],
    [Description],
    [DisplayFolder],
    [IsHidden],
    [State],
    [FormatString],
    [ExplicitDataType],
    [InferredDataType],
    [SourceProviderType],
    [DataCategory],
    [IsUnique],
    [IsKey],
    [IsNullable],
    [Alignment],
    [IsDefaultLabel],
    [IsDefaultImage],
    [SummarizeBy],
    [Type],
    [ColumnOriginID],
    [SourceColumn],
    [IsAvailableInMDX],
    [SortByColumnID],
    [SystemFlags],
    [KeepUniqueRows],
    [DisplayOrdinal],
    [ErrorMessage]
FROM $SYSTEM.TMSCHEMA_COLUMNS
ORDER BY [TableID] ASC
"
                 },
        new ModelAnalyzerTable()
                 {
                     TableName = "ColumnsStorages",
                     MinCompatibilityLevel = 1200,
                     Query = @"
SELECT
    [ColumnID],
    [StoragePosition],
    [DictionaryStorageID],
    [Settings],
    [ColumnFlags],
    [Collation],
    [OrderByColumn],
    [Locale],
    [BinaryCharacters],
    [Statistics_DistinctStates],
    [Statistics_MinDataID],
    [Statistics_MaxDataID],
    [Statistics_OriginalMinSegmentDataID],
    [Statistics_RLESortOrder],
    [Statistics_RowCount],
    [Statistics_HasNulls],
    [Statistics_RLERuns],
    [Statistics_OthersRLERuns],
    [Statistics_Usage],
    [Statistics_DBType],
    [Statistics_XMType],
    [Statistics_CompressionType],
    [Statistics_CompressionParam],
    [Statistics_EncodingHint]
FROM $SYSTEM.TMSCHEMA_COLUMN_STORAGES
ORDER BY [ColumnID] ASC
"
                 },
                new ModelAnalyzerTable()
                 {
                     TableName = "TablesMetadata",
                     MinCompatibilityLevel = 1200,
                     Query = @"
SELECT 
    [ID] AS [TableID], 
    [Name] AS [Table],
    [DataCategory],
    [Description],
    [IsHidden],
    [SystemFlags]
FROM $SYSTEM.TMSCHEMA_TABLES
ORDER BY [Name] ASC
"
                 },
                new ModelAnalyzerTable()
                 {
                     TableName = "Relationships",
                     MinCompatibilityLevel = 1200,
                     Query = @"
SELECT    
    [ID] AS [RelationshipID],
    [FromTableID],
    [FromColumnID],
    [FromCardinality] AS [FromCardinalityType],
    [ToTableID],
    [ToColumnID],
    [ToCardinality] AS [ToCardinalityType],
    [IsActive] AS [Active],
    [CrossFilteringBehavior],
    [JoinOnDateBehavior],
    [RelyOnReferentialIntegrity],
    [SecurityFilteringBehavior],
    [State]
FROM $SYSTEM.TMSCHEMA_RELATIONSHIPS
ORDER BY [FromTableID] ASC
"
                 },
                new ModelAnalyzerTable()
                 {
                     TableName = "TablesExpressions",
                     MinCompatibilityLevel = 1200,
                     Query = @"
SELECT 
    [TableID], 
    [Name] AS [Table],
    [Description],
    TRIM ( [QueryDefinition] ) AS [Expression],
    [State],
    [Mode],
    [DataView],
    [SystemFlags],
    [ErrorMessage]
FROM [$system].[TMSCHEMA_PARTITIONS]
WHERE [Type] = 2
ORDER BY [Name] ASC
"
                 },
                new ModelAnalyzerTable()
                 {
                     TableName = "Roles",
                     MinCompatibilityLevel = 1200,
                     Query = @"
SELECT 
    [ID] AS [RoleID],
    [Name] AS [Role],
    [Description],
    [ModelPermission]
FROM $SYSTEM.TMSCHEMA_ROLES
ORDER BY [Name] ASC
"
                 },
                new ModelAnalyzerTable()
                 {
                     TableName = "TablesPermissions",
                     MinCompatibilityLevel = 1200,
                     Query = @"
SELECT
    [ID] AS [PermissionID],
    [RoleID],
    [TableID],
    TRIM ( [FilterExpression] ) AS [FilterExpression],
    [State],
    [ErrorMessage]
FROM $SYSTEM.TMSCHEMA_TABLE_PERMISSIONS
ORDER BY [TableID] ASC
"
                 }
            };
        }

        public static DataSet Create(ADOTabularConnection cnn) {
            DataSet result = new DataSet();
            Dictionary<string, Exception> _processingExceptions = new Dictionary<string, Exception>();
            var db = GetAmoDatabase(cnn);

            AddDatabaseTable(db, result);

            foreach (var qry in GetQueries()) {
                // skip over this query if the compatibility level is not supported for the current database
                if (qry.MinCompatibilityLevel > db.CompatibilityLevel) continue;
                if (qry.MaxCompatibilityLevel < db.CompatibilityLevel) continue;
                try
                {
                    System.Diagnostics.Debug.WriteLine(String.Format("Processing Table - {0}", qry.TableName));
                    var dt = cnn.ExecuteDaxQueryDataTable(qry.Query);
                    dt.TableName = qry.TableName + "." + qry.MinCompatibilityLevel.ToString();
                    result.Tables.Add(dt);
                } catch (Exception ex)
                {
                    _processingExceptions.Add("Executing Query: " + qry.TableName, ex);
                }
            }

            if (_processingExceptions.Count != 0)
            {
                var msg = string.Join("/n", _processingExceptions.Keys.ToArray());
                var aggEx = new AggregateException("ModelAnalyzerException: " + msg, _processingExceptions.Values.ToArray());
                throw aggEx;
            }

            // replace some of the lookups between tables
            PostProcessColumnMetadata(result);
            PostProcessMeasureExpressions(result);
            PostProcessColumCardinality(result);
            PostProcessTables(result);
            PostProcessUnusedColumns(db, result);
            AddRelationshipsTable(db, result);

            

            return result;
        }

        private static void PostProcessColumnMetadata(DataSet result)
        {
            UpdateTableName(result, "ColumnsMetadata.1200");
        }

        private static void PostProcessMeasureExpressions(DataSet result)
        {
            var measureExpressions1100 = result.Tables["MeasuresExpressions.1100"];
            var measureExpressions1200 = result.Tables["MeasuresExpressions.1200"];
            // todo - union measureExpressions 1100 & 1200 and update name if null
            if (measureExpressions1200 != null && measureExpressions1200.Rows.Count > 0)
            {
                measureExpressions1200.TableName = "MeasureExpressions";
                UpdateTableName(result, "MeasureExpressions");
            } else
            {
                measureExpressions1100.TableName = "MeasureExpressions";
            }

        }

        private static void UpdateTableName(DataSet result, string tableName)
        {
            var tables = result.Tables["TablesMetadata.1200"];
            var tableToUpdate = result.Tables[tableName];
            foreach (DataRow row in tableToUpdate.Rows)
            {
                row["Table"] = tables.Select("TableID = " + row["TableID"].ToString())[0]["Table"];
            }
        }

        private static void PostProcessUnusedColumns(Microsoft.AnalysisServices.Database db, DataSet result)
        {
            // TODO Look for hidden columns, not used in sort by and not in hierarchies and not referenced in calcs
            //throw new NotImplementedException();
        }

        private static void AddDatabaseTable(Microsoft.AnalysisServices.Database db, DataSet result)
        {
            var dbTable = new DataTable("Model");
            dbTable.Columns.Add("Name", typeof(string));
            dbTable.Columns.Add("ID", typeof(string));
            dbTable.Columns.Add("CompatibilityLevel", typeof(Int32));
            dbTable.Columns.Add("LastProcessed", typeof(DateTime));
            dbTable.Columns.Add("LastSchemaUpdate", typeof(DateTime));
            dbTable.Columns.Add("EstimatedSize", typeof(long));
            var modelRow = dbTable.NewRow();
            modelRow["Name"] = db.Name;
            modelRow["ID"] = db.ID;
            modelRow["CompatibilityLevel"] = db.CompatibilityLevel;
            modelRow["LastProcessed"] = db.LastProcessed;
            modelRow["LastSchemaUpdate"] = db.LastSchemaUpdate;
            modelRow["EstimatedSize"] = db.EstimatedSize;
            dbTable.Rows.Add(modelRow);
        }

        private static void AddRelationshipsTable(Microsoft.AnalysisServices.Database db,DataSet result)
        {
            

            // Check that the database was found and is a tabular model database
            if (db == null) return;
            if (db.ModelType != ModelType.Tabular) return;


            if (db.Model != null)
                ProcessTabularRelationships(db.Model, result);
            else
                ProcessMdRelationships(db, result);

        }

        private static Microsoft.AnalysisServices.Database GetAmoDatabase(ADOTabularConnection cnn)
        {
            var serverName = cnn.ServerName;

            var server = new Microsoft.AnalysisServices.Server();
            server.Connect(serverName);
            var db = server.Databases.GetByName(cnn.Database.Name);
            return db;
        }

        private static void ProcessMdRelationships(Microsoft.AnalysisServices.Database db, DataSet result)
        {
            var rels = new List<ModelAnalyzerRelationship>();
            foreach (Dimension dim in db.Dimensions)
            {
                foreach (var scanRel in dim.Relationships)
                {
                    // Check whether the relationship is a known type, otherwise skips it
                    Microsoft.AnalysisServices.Relationship rel = scanRel as Microsoft.AnalysisServices.Relationship;
                    if (rel != null) {
                        rels.Add(new ModelAnalyzerRelationship(rel));
                    }
                }
            }

            var relationshipsTable = rels.ToDataTable();
            relationshipsTable.TableName = "Relationships";
            result.Tables.Add(relationshipsTable);
        }

        private static void ProcessTabularRelationships(Microsoft.AnalysisServices.Tabular.Model model,DataSet result)
        {
            var rels = new List<ModelAnalyzerRelationship>();
            foreach (SingleColumnRelationship scanRel in model.Relationships)
            {
                // Check whether the relationship is a known type, otherwise skips it
                SingleColumnRelationship rel = scanRel as SingleColumnRelationship;
                if (rel != null) {
                    rels.Add(new ModelAnalyzerRelationship(rel));
                }
            }
            
            var relationshipsTable = rels.ToDataTable();
            relationshipsTable.TableName = "Relationships";
            result.Tables.Add(relationshipsTable);
        }

        private static void PostProcessTables(DataSet result)
        {
            var tableTable = result.Tables["Tables.1100"];
            var columnTable = result.Tables["Columns.1100"];
            foreach (DataRow tableRow in tableTable.Rows)
            {
                // TODO - I'm not sure the following code is necessary or we use the PostProcessColumCardinality instead
                var filterExpression = string.Format("TABLE_NAME = '{0}' and COLUMN_ID LIKE 'RowNumber %'", tableRow["TABLE_NAME"]);
                var row = columnTable.Select(filterExpression).FirstOrDefault();
                
                if (row != null)
                    row["COLUMN_CARDINALITY"] = (long)tableRow["ROWS_IN_TABLE"];
            }
        }

        private static void PostProcessColumCardinality(DataSet result)
        {
            var columnTable = result.Tables["Columns.1100"];
            var columnCardinalityTable = result.Tables["ColumnsCardinality.1100"];

            columnTable.Columns.Add(new System.Data.DataColumn("COLUMN_CARDINALITY", typeof(long)));
            
            foreach (DataRow columnRow in columnCardinalityTable.Rows)
            {
                // TODO: We might get the COLUMN_CARDINALITY from the Columns Storages table if available, 
                //       so we keep compatibility in case IsAvailableInMDX is set to false and column hierarchy is not available
                //       by doing this we can remove the calculated column in VertiPaq Analyzer to compute the right estimate
                //       Marco - 2018-05-22
                string columnName = columnRow["COLUMN_HIERARCHY_ID"].ToString();
                columnName = columnName.Split('$')[2];
                var filterExpression = string.Format("TABLE_NAME = '{0}' and COLUMN_ID = '{1}'", columnRow["TABLE_NAME"], columnName);
                var row = columnTable.Select(filterExpression).FirstOrDefault();
                if (row != null)
                    row["COLUMN_CARDINALITY"] = columnRow["COLUMN_CARDINALITY"];
                else
                {
                    // TODO - I'm not sure the following code is necessary or we use the PostProcessTables instead
                    filterExpression = string.Format("TABLE_NAME = '{0}' and COLUMN_ID LIKE 'RowNumber %'", columnRow["TABLE_NAME"]);
                    row = columnTable.Select(filterExpression).FirstOrDefault();
                    var table = result.Tables["Tables"].Select(String.Format("TABLE_NAME = '{0}'", columnRow["TABLE_NAME"].ToString())).FirstOrDefault();
                    if (row != null && table != null)
                        row["COLUMN_CARDINALITY"] = (long)table["ROWS_IN_TABLE"];
                }
            }
        }

        
    }
}
