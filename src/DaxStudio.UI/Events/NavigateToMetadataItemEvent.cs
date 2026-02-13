namespace DaxStudio.UI.Events
{
    /// <summary>
    /// Event to navigate to and select an item in the metadata pane.
    /// </summary>
    public class NavigateToMetadataItemEvent
    {
        public string TableName { get; }
        public string ColumnName { get; }

        /// <summary>
        /// Navigate to a table in the metadata pane.
        /// </summary>
        public NavigateToMetadataItemEvent(string tableName)
        {
            TableName = tableName;
            ColumnName = null;
        }

        /// <summary>
        /// Navigate to a column within a table in the metadata pane.
        /// </summary>
        public NavigateToMetadataItemEvent(string tableName, string columnName)
        {
            TableName = tableName;
            ColumnName = columnName;
        }
    }
}
