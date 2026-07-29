using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DaxStudio.Core.Model
{
    /// <summary>
    /// A WPF-free node used to feed the tree-grid shown in the Results pane by the
    /// Comment Script <c>--&gt; SHOW</c> commands (DEPENDENCIES / LAST_UPDATED / MAX_UPDATED).
    /// Lives in Core so the connection layer can build the tree without referencing the UI;
    /// the tree-grid binds directly to these nodes via <c>Data.*</c> and <c>Children</c>.
    /// Implements <see cref="INotifyPropertyChanged"/> (a BCL interface, so this stays WPF-free) so
    /// the tree-grid's many per-row bindings use WPF's weak PropertyChangedEventManager instead of the
    /// leaking PropertyDescriptor.AddValueChanged fallback WPF uses for plain CLR objects.
    /// </summary>
    public class ShowTreeNode : INotifyPropertyChanged
    {
        public ShowTreeNode(string name, string objectType, string tableName = null, DateTime? lastModifiedUtc = null, bool isFolder = false)
        {
            _name = name;
            _objectType = objectType;
            _tableName = tableName;
            _lastModifiedUtc = lastModifiedUtc;
            _isFolder = isFolder;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private string _name;
        /// <summary>The object name (measure, column, table or function name). For folder nodes this is
        /// the group label (e.g. "Measures") without the count - see <see cref="DisplayName"/>.</summary>
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        private string _objectType;
        /// <summary>The object type (MEASURE, COLUMN, TABLE, FUNCTION, PARTITION, ...). Blank for folder
        /// group nodes and "MODEL" for the root semantic-model node.</summary>
        public string ObjectType
        {
            get => _objectType;
            set { _objectType = value; OnPropertyChanged(); }
        }

        private string _tableName;
        /// <summary>The owning table name, when applicable (blank for tables / model-level objects / folders).</summary>
        public string TableName
        {
            get => _tableName;
            set { _tableName = value; OnPropertyChanged(); }
        }

        private DateTime? _lastModifiedUtc;
        /// <summary>The last schema-modified (or refreshed) time in UTC, when known.</summary>
        public DateTime? LastModifiedUtc
        {
            get => _lastModifiedUtc;
            set { _lastModifiedUtc = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastModifiedDisplay)); }
        }

        private string _expression;
        /// <summary>The DAX expression (body) of the object, when available. Populated for MEASURE nodes
        /// (from the model measures) and FUNCTION nodes (model user-defined functions from the
        /// TMSCHEMA_FUNCTIONS DMV, or query-scoped DEFINE FUNCTION definitions). Blank for objects that
        /// carry no expression (columns, tables, folders, ...).</summary>
        public string Expression
        {
            get => _expression;
            set { _expression = value; OnPropertyChanged(); }
        }

        private bool _isFolder;
        /// <summary>True for the synthetic grouping folders (Measures, Columns, Tables, ...) that mirror
        /// the Power BI Desktop model view. Folders carry no timestamp of their own.</summary>
        public bool IsFolder
        {
            get => _isFolder;
            set { _isFolder = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        private DateTime? _maxUpdateUtc;
        /// <summary>The most-recent modified time across all descendant objects, when known. Rolled up
        /// during tree construction. For individual leaf items this instead holds the item's own timestamp
        /// when it carries the newest change within its container folder; otherwise blank.</summary>
        public DateTime? MaxUpdateUtc
        {
            get => _maxUpdateUtc;
            set { _maxUpdateUtc = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaxUpdateDisplay)); }
        }

        private int? _daysSinceChange;
        /// <summary>Whole days between now and the row's effective most-recent change (its own timestamp
        /// rolled up with its descendants'). Null when no timestamp is available anywhere in the subtree.</summary>
        public int? DaysSinceChange
        {
            get => _daysSinceChange;
            set { _daysSinceChange = value; OnPropertyChanged(); OnPropertyChanged(nameof(DaysSinceChangeDisplay)); }
        }

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
