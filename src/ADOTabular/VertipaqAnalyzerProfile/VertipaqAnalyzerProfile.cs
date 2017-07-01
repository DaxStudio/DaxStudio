using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.VertipaqAnalyzerProfile
{


    public static class VertipaqAnalyzerProfiler
    {
        private class VertipaqAnalyzerTable
        {
            public string TableName;
            public string Query;

        }

        private static List<VertipaqAnalyzerTable> GetQueries()
        {
            return new List<VertipaqAnalyzerTable>
            {
                new VertipaqAnalyzerTable()
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
                , new VertipaqAnalyzerTable()
                {
                    TableName = "Columns Cardinality",
                    Query = @"SELECT
    DIMENSION_NAME AS TABLE_NAME, 
    TABLE_ID AS COLUMN_HIERARCHY_ID,
    ROWS_COUNT - 3 AS COLUMN_CARDINALITY
FROM $SYSTEM.DISCOVER_STORAGE_TABLES
WHERE LEFT ( TABLE_ID, 2 ) = 'H$'
ORDER BY TABLE_ID"
                }
                , new VertipaqAnalyzerTable()
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
                , new VertipaqAnalyzerTable()
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
WHERE RIGHT ( LEFT ( TABLE_ID, 2 ), 1 ) <> '$'"
                }
                , new VertipaqAnalyzerTable()
                {
                    TableName = "Columns Hierarchies",
                    Query = @"SELECT 
    DIMENSION_NAME AS TABLE_NAME, 
    COLUMN_ID AS STRUCTURE_NAME,
    SEGMENT_NUMBER, 
    TABLE_PARTITION_NUMBER, 
    USED_SIZE,
    TABLE_ID AS COLUMN_HIERARCHY_ID
FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS
WHERE LEFT ( TABLE_ID, 2 ) = 'H$'"
                },
                new VertipaqAnalyzerTable()
                {
                    TableName = "User Hierarchies",
                    Query = @"SELECT 
    DIMENSION_NAME AS TABLE_NAME, 
    COLUMN_ID AS STRUCTURE_NAME,
    USED_SIZE,
    TABLE_ID AS HIERARCHY_ID
FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS
WHERE LEFT ( TABLE_ID, 2 ) = 'U$'"
                }
                , new VertipaqAnalyzerTable()
                {
                    TableName = "Relationships",
                    Query = @"SELECT 
    DIMENSION_NAME AS TABLE_NAME, 
    USED_SIZE,
    TABLE_ID AS RELATIONSHIP_ID
FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS
WHERE LEFT ( TABLE_ID, 2 ) = 'R$'"
                },
                new VertipaqAnalyzerTable()
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
                ,new VertipaqAnalyzerTable()
                 {
                     TableName = "Measures Expressions",
                     Query = @"SELECT DISTINCT [MEASUREGROUP_NAME] as [Table], [MEASURE_NAME] as [Measure] , '=' + [EXPRESSION] as [DAX Expression], [DESCRIPTION] as [Description]   
FROM $SYSTEM.MDSCHEMA_MEASURES 
WHERE MEASURE_NAME <> '__XL_Count of Models'
AND MEASURE_NAME <> '__Default measure'
AND EXPRESSION > '' 
ORDER BY [MEASUREGROUP_NAME] + '_' + [MEASURE_NAME] ASC
"
                 }
                 ,new VertipaqAnalyzerTable()
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
            };
        }

        public static DataSet Create(ADOTabularConnection cnn)
        {
            DataSet result = new DataSet();

            foreach (var qry in GetQueries())
            {
                System.Diagnostics.Debug.WriteLine("Processing Table - {0}", qry.TableName);
                var dt = cnn.ExecuteDaxQueryDataTable(qry.Query);
                dt.TableName = qry.TableName;
                result.Tables.Add(dt);
            }

            return result;
        }
    }
}
