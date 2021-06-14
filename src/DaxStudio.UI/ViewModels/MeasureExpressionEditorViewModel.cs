using ADOTabular;
using ADOTabular.Interfaces;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using DaxStudio.UI.Views;
using GongSolutions.Wpf.DragDrop;
using ICSharpCode.AvalonEdit.Document;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Windows;
using DaxStudio.UI.Utils.Intellisense;
using UnitComboLib.Unit.Screen;
using UnitComboLib.ViewModel;
using DaxStudio.UI.Events;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class MeasureExpressionEditorViewModel:Screen
        ,IViewAware
        ,IDropTarget

    {
        private  IEventAggregator EventAggregator { get; }

        private DocumentViewModel Document { get; }
        public IGlobalOptions Options { get; }
        public TextDocument MeasureExpression { get; set; } = new TextDocument();
        
        public DaxIntellisenseProvider IntellisenseProvider { get; set; }
        public UnitViewModel SizeUnitLabel { get; set; }

        private string _measureName = string.Empty;
        public string MeasureName { get => _measureName;
            set { 
                _measureName = value;
                NotifyOfPropertyChange();    
            } 
        }

        public IADOTabularObject SelectedTable { get => _column?.SelectedTable;
            set {
                _column.SelectedTable = value;
                NotifyOfPropertyChange();
            } 
        }

        public ADOTabularTableCollection Tables => Document?.Connection?.SelectedModel?.Tables;

        private bool _isModelItem;
        public bool IsModelItem { get => _isModelItem;
            internal set {
                _isModelItem = value;
                NotifyOfPropertyChange();
            } 
        }

        private QueryBuilderColumn _column;
        public QueryBuilderColumn Column { get => _column;
            internal set {
                _column = value;
                NotifyOfPropertyChange(nameof(Tables));
                MeasureExpression.Text = _column.MeasureExpression??string.Empty;
                MeasureName = _column.Caption;
                SelectedTable = _column.SelectedTable;
                IsModelItem = _column.IsModelItem;
                
                NotifyOfPropertyChange(nameof(SelectedTable));

            } 
        }

        [ImportingConstructor]
        public MeasureExpressionEditorViewModel(DocumentViewModel document, IEventAggregator eventAggregator, IGlobalOptions options)
        {
            EventAggregator = eventAggregator;
            Document = document;
            Options = options;
            IntellisenseProvider = new DaxIntellisenseProvider(Document, EventAggregator, Options);
            
            
            var items = new ObservableCollection<UnitComboLib.ViewModel.ListItem>(ScreenUnitsHelper.GenerateScreenUnitList());
            SizeUnitLabel = new UnitViewModel(items, new ScreenConverter(Options.EditorFontSizePx), 0);
        }

        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            _editor = GetEditor();

            // TODO - if theme is dark increase brightness of syntax highlights
            //_editor.ChangeColorBrightness(1.25);
            _editor.SetSyntaxHighlightColorTheme(Options.Theme);

            IntellisenseProvider.Editor = _editor;
            UpdateSettings();
            if (_editor != null)
            {
                //FindReplaceDialog.Editor = _editor;
                //SetDefaultHighlightFunction();
                //_editor.TextArea.Caret.PositionChanged += OnPositionChanged;
                //_editor.TextChanged += OnDocumentChanged;
                //_editor.PreviewDrop += OnDrop;
                //_editor.PreviewDragEnter += OnDragEnterPreview;

                _editor.OnPasting += OnPasting;

            }
        }

        public void SaveMeasureExpression()
        {
            Document.ShowMeasureExpressionEditor = false;
            // TODO - get measure name 
            Column.MeasureExpression = MeasureExpression.Text;
            Column.Caption = MeasureName;
            Document.QueryBuilder.IsEnabled = true;
            EventAggregator.PublishOnUIThread(new QueryBuilderUpdateEvent());
        }

        public void CancelMeasureExpression()
        {
            Document.ShowMeasureExpressionEditor = false;
            if (string.IsNullOrWhiteSpace(this.Column.MeasureExpression) && !this.Column.IsModelItem )
            {
                // if the existing measure expression is empty and it's not a model item, 
                // then we have hit cancel on a custom measure so we should remove it from the list
                Document.QueryBuilder.Columns.Remove(this.Column);
            }
            Document.QueryBuilder.IsEnabled = true;
        }

        private void OnPasting(object sender, DataObjectPastingEventArgs e)
        {

            try
            {
                // this check strips out unicode non-breaking spaces and replaces them
                // with a "normal" space. This is helpful when pasting code from other 
                // sources like web pages or word docs which may have non-breaking
                // which would normally cause the tabular engine to throw an error
                string content = e.DataObject.GetData("UnicodeText", true) as string;
                var dataObject = new DataObject(content.Replace('\u00A0', ' '));
                e.DataObject = dataObject;

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while Pasting: {message}", ex.Message);
                OutputError($"Error while Pasting: {ex.Message}");
            }

        }

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            IntellisenseProvider?.CloseCompletionWindow();

            if (dropInfo.DragInfo.DataObject is IADOTabularObject || dropInfo.DragInfo.Data is string)
            {
                dropInfo.Effects = DragDropEffects.Move;
                var pt = dropInfo.DropPosition;
                var pos = _editor.GetPositionFromPoint(pt);
                if (!pos.HasValue) return;
                var off = _editor.Document.GetOffset(pos.Value.Location);
                _editor.CaretOffset = off;
                _editor.Focus();
            }
            else
            {
                dropInfo.Effects = DragDropEffects.None;
            }
        }

        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            var obj = dropInfo.DragInfo.DataObject as IADOTabularObject;
            var text = string.Empty;
            if (obj != null)
            {
                text = obj.DaxName;
            }

            if (dropInfo.DragInfo.Data is string)
            {
                text = dropInfo.DragInfo.Data as string;
            }
            InsertTextAtCaret(text);
            return;
        }

        private void InsertTextAtCaret(string text)
        {
            var editor = GetEditor();
            editor.Document.Insert(editor.CaretOffset, text);
            editor.Focus();
        }

        private DAXEditorControl.DAXEditor _editor;
        private DAXEditorControl.DAXEditor GetEditor()
        {
            MeasureExpressionEditorView v = (MeasureExpressionEditorView)GetView();
            return v?.daxExpressionEditor;
        }

        public void OutputError(string error)
        {
            Document.OutputPane.AddError(error, double.NaN);
        }

        public bool ConvertTabsToSpaces => Options.EditorConvertTabsToSpaces;
        public int IndentationSize => Options.EditorIndentationSize;
        public bool WordWrap => Options.EditorWordWrap;
        public bool IsFocused { get; set; }
        public void UpdateSettings()
        {
            var editor = GetEditor();

            if (editor == null) return;

            if (editor.ShowLineNumbers != Options.EditorShowLineNumbers)
            {
                editor.ShowLineNumbers = Options.EditorShowLineNumbers;
            }
            if (editor.FontFamily.Source != Options.EditorFontFamily)
            {
                editor.FontFamily = new System.Windows.Media.FontFamily(Options.EditorFontFamily);
            }
            if (editor.FontSize != Options.EditorFontSizePx)
            {
                editor.FontSize = Options.EditorFontSizePx;
                this.SizeUnitLabel.SetOneHundredPercentFontSize(Options.EditorFontSizePx);
                this.SizeUnitLabel.StringValue = "100";
            }

            if (Options.EditorEnableIntellisense)
            {
                editor.EnableIntellisense(IntellisenseProvider);
            }
            else
            {
                editor.DisableIntellisense();
            }

            NotifyOfPropertyChange(nameof(ConvertTabsToSpaces));
            NotifyOfPropertyChange(nameof(IndentationSize));
            NotifyOfPropertyChange(nameof(WordWrap));
        }

    }
}
