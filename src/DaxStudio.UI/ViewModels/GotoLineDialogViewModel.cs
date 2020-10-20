using DAXEditorControl;
using System.Media;
using Caliburn.Micro;
using DaxStudio.UI.Extensions;
using System.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DaxStudio.UI.ViewModels
{
    public class GotoLineDialogViewModel : Screen, INotifyDataErrorInfo
    {
        private int _lineNo;
        private int _maxLines;
        private readonly Dictionary<string, ICollection<string>>
            _validationErrors = new Dictionary<string, ICollection<string>>();
        private IEditor editor;

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public GotoLineDialogViewModel(IEditor editor)
        {
            this.Editor = editor;

        }

        #region Properties

        public IEditor Editor { get { return editor; } set {
                editor = value;
                MaxLines = Editor.TextArea.Document.LineCount;
            }
        }

        public int LineNo
        {
            get { return _lineNo; }
            set
            {
                _lineNo = value;
                ValidateLineNo(LineNo);
                NotifyOfPropertyChange(() => LineNo);
                NotifyOfPropertyChange(() => IsValidLineNo);
            }
        }

        public int MaxLines
        { get { return _maxLines; }
            set {
                _maxLines = value;
                NotifyOfPropertyChange(() => MaxLines);
            }
        }

        public bool IsValidLineNo
        {
            get { return LineNo <= MaxLines && LineNo >= 0; }
        }

        

        //private void FocusOnFindBox()
        //{
        //    var v = (FindReplaceDialogView)this.GetView();
        //    v.FocusOnFind();
        //}
        public bool IsFocused { get { return true; } }

        #region INotifyDataErrorInfo members
        public bool HasErrors
        {
            get
            {
                return _validationErrors.Count > 0;
            }
        }

        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)
            || !_validationErrors.ContainsKey(propertyName))
                return null;

            return _validationErrors[propertyName];
        }

        
        private void RaiseErrorsChanged(string propertyName)
        {
            if (ErrorsChanged != null)
                ErrorsChanged(this, new DataErrorsChangedEventArgs(propertyName));
        }
        #endregion

#endregion


        #region Methods

        public void GotoLine()
        {
            if (editor == null || LineNo == 0)
            {
                SystemSounds.Beep.Play();
                return;
            }
            if (!GotoLineInternal())
                SystemSounds.Beep.Play();
            else
            {
                Editor.TextArea.Focus();
                this.TryClose();
            }
        }

        
        public void Cancel()
        {
            this.TryClose();
        }


        private bool GotoLineInternal()
        {
            if (editor != null)
            {
                // TODO - goto line
                if (  LineNo <= Editor.TextArea.Document.LineCount)
                {
                    //Editor.TextArea.Caret.Position
                    Editor.TextArea.Caret.Line = LineNo;
                    Editor.TextArea.Caret.BringCaretToView();
                    return true;
                }
                return false;
            }
            return false;
        }

        private void ValidateLineNo(int lineNo)
        {
            const string propertyKey = "LineNo";
            var isValid = lineNo >= 1 && lineNo <= MaxLines;
            if (!isValid)
            {
                _validationErrors[propertyKey] = new List<string>() { $"Line must be between 1 and {MaxLines}" };
                RaiseErrorsChanged(propertyKey);
            }
            else if (_validationErrors.ContainsKey(propertyKey))
            {
                _validationErrors.Remove(propertyKey);
                RaiseErrorsChanged(propertyKey);
            }
        }

        #endregion

    }
}
