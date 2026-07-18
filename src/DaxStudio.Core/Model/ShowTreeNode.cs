using System;
using System.Collections.Generic;

namespace DaxStudio.Core.Model
{
    /// <summary>
    /// A WPF-free node used to feed the tree-grid shown in the Results pane by the
    /// Comment Script <c>--&gt; SHOW</c> commands (DEPENDENCIES / LAST_UPDATED / MAX_UPDATED).
    /// Lives in Core so the connection layer can build the tree without referencing the UI;
    /// the tree-grid binds directly to these nodes via <c>Data.*</c> and <c>Children</c>.
    /// </summary>
    public class ShowTreeNode
    {
        public ShowTreeNode(string name, string objectType, string tableName = null, DateTime? lastModifiedUtc = null, bool isFolder = false)
        {
            Name = name;
            ObjectType = objectType;
            TableName = tableName;
            LastModifiedUtc = lastModifiedUtc;
            IsFolder = isFolder;
        }

        /// <summary>The object name (measure, column, table or function name). For folder nodes this is
        /// the group label (e.g. "Measures") without the count - see <see cref="DisplayName"/>.</summary>
        public string Name { get; set; }

        /// <summary>The object type (MEASURE, COLUMN, TABLE, FUNCTION, PARTITION, ...). Blank for folder
        /// group nodes and "MODEL" for the root semantic-model node.</summary>
        public string ObjectType { get; set; }

        /// <summary>The owning table name, when applicable (blank for tables / model-level objects / folders).</summary>
        public string TableName { get; set; }

        /// <summary>The last schema-modified (or refreshed) time in UTC, when known.</summary>
        public DateTime? LastModifiedUtc { get; set; }

        /// <summary>True for the synthetic grouping folders (Measures, Columns, Tables, ...) that mirror
        /// the Power BI Desktop model view. Folders carry no timestamp of their own.</summary>
        public bool IsFolder { get; set; }

        /// <summary>The most-recent modified time across all descendant objects, when known. Rolled up
        /// during tree construction. Blank for leaf nodes with no children.</summary>
        public DateTime? MaxUpdateUtc { get; set; }

        /// <summary>Whole days between now and the row's effective most-recent change (its own timestamp
        /// rolled up with its descendants'). Null when no timestamp is available anywhere in the subtree.</summary>
        public int? DaysSinceChange { get; set; }

        /// <summary>Local-time display string for <see cref="LastModifiedUtc"/> (blank when null).</summary>
        public string LastModifiedDisplay =>
            LastModifiedUtc.HasValue ? LastModifiedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;

        /// <summary>Local-time display string for <see cref="MaxUpdateUtc"/> (blank when null).</summary>
        public string MaxUpdateDisplay =>
            MaxUpdateUtc.HasValue ? MaxUpdateUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;

        /// <summary>Display string for <see cref="DaysSinceChange"/> (blank when null).</summary>
        public string DaysSinceChangeDisplay =>
            DaysSinceChange.HasValue ? DaysSinceChange.Value.ToString() : string.Empty;

        /// <summary>The text shown in the tree Object column. Folder nodes append their live child count
        /// (e.g. "Measures (9)") so the number stays correct even after the MAX_UPDATED prune.</summary>
        public string DisplayName => IsFolder ? $"{Name} ({Children.Count})" : Name;

        /// <summary>Child nodes. Bound as the tree-grid ChildrenBindingPath.</summary>
        public List<ShowTreeNode> Children { get; } = new List<ShowTreeNode>();
    }
}
