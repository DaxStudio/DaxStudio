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

        private class ModelAnalyzerRelationship
        {
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
        }

        private static List<ModelAnalyzerTable> GetQueries()
        {
            return new List<ModelAnalyzerTable>
            {
                new ModelAnalyzerTable()
                {
                    TableName = "Tables",
                    Query = @"SELECT 
    DIMENSION_NAME AS TABLE_NAME, 
 TABLE_ID AS TABLE_ID,
    ROWS_COUNT AS ROWS_IN_TABLE
FROM  $SYSTEM.DISCOVER_STORAGE_TABLES
WHERE RIGHT ( LEFT ( TABLE_ID, 2 ), 1 ) <> '$'
ORDER BY DIMENSION_NAME"
                }
                , new ModelAnalyzerTable()
                {
                    TableName = "Columns Cardinality",
                    MinCompatibilityLevel = 1100,
                    Query = @"SELECT
    DIMENSION_NAME AS TABLE_NAME, 
    TABLE_ID AS COLUMN_HIERARCHY_ID,
    ROWS_COUNT - 3 AS COLUMN_CARDINALITY
FROM $SYSTEM.DISCOVER_STORAGE_TABLES
WHERE LEFT ( TABLE_ID, 2 ) = 'H$'
ORDER BY TABLE_ID"
                }
                , new ModelAnalyzerTable()
                {
                    TableName = "Columns",
                    Query = @"SELECT
    DIMENSION_NAME AS TABLE_NAME, 
    COLUMN_ID AS COLUMN_ID, 
    ATTRIBUTE_NAME AS COLUMN_NAME, 
    DATATYPE AS [Data Type],
    DICTIONARY_SIZE AS DICTIONARY_SIZE_BYTES,
    COLUMN_ENCODING AS COLUMN_ENCODING_INT
FROM  $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMNS
WHERE COLUMN_TYPE = 'BASIC_DATA'"
                }
                , new ModelAnalyzerTable()
                {
                    TableName = "Columns Segments",
                    Query = @"SELECT 
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
--WHERE RIGHT ( LEFT ( TABLE_ID, 2 ), 1 ) <> '$'
"
                },
//                new ModelAnalyzerTable()
//                {
//                    TableName = "Columns Hierarchies",
//                    Query = @"SELECT 
//    DIMENSION_NAME AS TABLE_NAME, 
//    COLUMN_ID AS STRUCTURE_NAME,
//    SEGMENT_NUMBER, 
//    TABLE_PARTITION_NUMBER, 
//    USED_SIZE,
//    TABLE_ID AS COLUMN_HIERARCHY_ID
//FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS
//WHERE LEFT ( TABLE_ID, 2 ) = 'H$'"
//                },
//                new ModelAnalyzerTable()
//                {
//                    TableName = "User Hierarchies",
//                    Query = @"SELECT 
//    DIMENSION_NAME AS TABLE_NAME, 
//    COLUMN_ID AS STRUCTURE_NAME,
//    USED_SIZE,
//    TABLE_ID AS HIERARCHY_ID
//FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS
//WHERE LEFT ( TABLE_ID, 2 ) = 'U$'"
//                }
//                , new ModelAnalyzerTable()
//                {
//                    TableName = "Relationship Storage",
//                    Query = @"SELECT 
//    DIMENSION_NAME AS TABLE_NAME, 
//    USED_SIZE,
//    TABLE_ID AS RELATIONSHIP_ID
//FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS
//WHERE LEFT ( TABLE_ID, 2 ) = 'R$'"
//                },
                new ModelAnalyzerTable()
                {
                TableName = "Table Names",
                Query = @"SELECT 
    RIGHT ( TABLE_SCHEMA, LEN ( TABLE_SCHEMA ) - 1 ) AS TABLE_NAME,
    TABLE_NAME AS TABLE_ID
FROM $SYSTEM.DBSCHEMA_TABLES
WHERE TABLE_TYPE = 'SYSTEM TABLE'
  AND LEFT(TABLE_SCHEMA,1)='$'
"
                }
                ,new ModelAnalyzerTable()
                 {
                     TableName = "Measures Expressions",
                     Query = @"SELECT DISTINCT 
    [MEASUREGROUP_NAME] as [Table], 
    [MEASURE_NAME] as [Measure], 
    '=' + [EXPRESSION] as [DAX Expression], 
    [DESCRIPTION] as [Description],
    [MEASURE_IS_VISIBLE]   
FROM $SYSTEM.MDSCHEMA_MEASURES 
WHERE MEASURE_NAME <> '__XL_Count of Models'
AND MEASURE_NAME <> '__Default measure'
AND EXPRESSION > '' 
ORDER BY [MEASUREGROUP_NAME] + '_' + [MEASURE_NAME] ASC
"
                 }
                 ,new ModelAnalyzerTable()
                 {
                     TableName = "Columns Expressions",
                     Query = @"SELECT DISTINCT  
    [TABLE] AS [Table], 
    [OBJECT] as [Column], 
    TRIM( '=' +  [EXPRESSION] ) as [DAX Expression],
    '' AS [Description]
FROM $SYSTEM.DISCOVER_CALC_DEPENDENCY  
WHERE OBJECT_TYPE = 'CALC_COLUMN'  
ORDER BY [TABLE]+[OBJECT]"
                 }
                 , new ModelAnalyzerTable()
                 {
                     TableName = "TMSCHEMA Tables",
                     MinCompatibilityLevel = 1200,
                     Query = @"SELECT 
[ID],
[Name],
IsHidden
FROM $SYSTEM.TMSCHEMA_TABLES
"
                 }
                 , new ModelAnalyzerTable()
                 {
                     TableName = "TMSCHEMA Columns",
                     MinCompatibilityLevel = 1200,
                     Query = @"SELECT 
[ID],
TableID,
ExplicitName,
IsHidden,
Expression,
SortByColumnID
FROM $SYSTEM.TMSCHEMA_COLUMNS
"
                 }
            };
        }

        public static DataSet Create(ADOTabularConnection cnn)
        {
            DataSet result = new DataSet();
            var db = GetAmoDatabase(cnn);

            AddDatabaseTable(db, result);

            foreach (var qry in GetQueries())
            {
                // skip over this query if the compatibility level is higher than the current database
                if (qry.MinCompatibilityLevel > db.CompatibilityLevel) continue;

                System.Diagnostics.Debug.WriteLine("Processing Table - {0}", qry.TableName);
                var dt = cnn.ExecuteDaxQueryDataTable(qry.Query);
                dt.TableName = qry.TableName;
                result.Tables.Add(dt);
            }

            // replace some of the lookups between tables
            PostProcessColumCardinality(result);
            PostProcessTables(result);
            PostProcessUnusedColumns(db, result);
            AddRelationshipsTable(db, result);
            
            return result;
        }

        private static void PostProcessUnusedColumns(Microsoft.AnalysisServices.Database db, DataSet result)
        {
            // Look for hidden columns, not used in sort by and not in hierarchies and not referenced in calcs
            throw new NotImplementedException();
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
                foreach (SingleColumnRelationship rel in dim.Relationships)
                {
                    rels.Add(new ModelAnalyzerRelationship(rel));
                }
            }

            var relationshipsTable = rels.ToDataTable();
            relationshipsTable.TableName = "Relationships";
            result.Tables.Add(relationshipsTable);
        }

        private static void ProcessTabularRelationships(Microsoft.AnalysisServices.Tabular.Model model,DataSet result)
        {
            var rels = new List<ModelAnalyzerRelationship>();
            foreach (SingleColumnRelationship rel in model.Relationships)
            {
                rels.Add(new ModelAnalyzerRelationship(rel));
            }
            
            var relationshipsTable = rels.ToDataTable();
            relationshipsTable.TableName = "Relationships";
            result.Tables.Add(relationshipsTable);
        }

        private static void PostProcessTables(DataSet result)
        {
            var tableTable = result.Tables["Tables"];
            var columnTable = result.Tables["Columns"];
            foreach (DataRow tableRow in tableTable.Rows)
            {
                var filterExpression = string.Format("TABLE_NAME = '{0}' and COLUMN_ID LIKE 'RowNumber %'", tableRow["TABLE_NAME"]);
                var row = columnTable.Select(filterExpression).FirstOrDefault();
                
                if (row != null)
                    row["RowCount"] = (long)tableRow["ROWS_IN_TABLE"];
            }
        }

        private static void PostProcessColumCardinality(DataSet result)
        {
            var columnTable = result.Tables["Columns"];
            var columnCardinalityTable = result.Tables["Columns Cardinality"];

            columnTable.Columns.Add(new System.Data.DataColumn("RowCount", typeof(long)));
            
            foreach (DataRow columnRow in columnCardinalityTable.Rows)
            {
                string columnName = columnRow["COLUMN_HIERARCHY_ID"].ToString();
                columnName = columnName.Split('$')[2];
                var filterExpression = string.Format("TABLE_NAME = '{0}' and COLUMN_ID = '{1}'", columnRow["TABLE_NAME"], columnName);
                var row = columnTable.Select(filterExpression).FirstOrDefault();
                if (row != null)
                    row["RowCount"] = columnRow["COLUMN_CARDINALITY"];
                else
                {
                    filterExpression = string.Format("TABLE_NAME = '{0}' and COLUMN_ID LIKE 'RowNumber %'", columnRow["TABLE_NAME"]);
                    row = columnTable.Select(filterExpression).FirstOrDefault();
                    var table = result.Tables["Tables"].Select(String.Format("TABLE_NAME = '{0}'", columnRow["TABLE_NAME"].ToString())).FirstOrDefault();
                    if (row != null && table != null)
                        row["RowCount"] = (long)table["ROWS_IN_TABLE"];
                }
            }
        }
    }
}
