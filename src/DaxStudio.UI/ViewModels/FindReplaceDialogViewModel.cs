using Caliburn.Micro;
using ICSharpCode.AvalonEdit.Document;
using DAXEditorControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Text.RegularExpressions;
using DaxStudio.UI.Extensions;
using Serilog;
using DaxStudio.UI.Events;
using DaxStudio.Common;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Threading;
using ICSharpCode.AvalonEdit.Rendering;



namespace DaxStudio.UI.ViewModels
{
    public enum SearchDirection
    {
        Next,
        Prev
    }
        
    public class FindReplaceDialogViewModel : Screen, IViewAware
    {


        private static string _textToFind = "";
        private static string _textToReplace = "";
        private static bool _caseSensitive;
        private static bool _useRegex;
        private static bool _useWildcards;
        private static bool _searchUp;
        private static bool _useWholeWord;
        private readonly IEventAggregator _eventAggregator;

        public FindReplaceDialogViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            IsVisible = false;
        }

        protected override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(100); // let the screen draw for the first time before we try and set the focus
            this.SetFocus(() => TextToFind);
        }
        
        #region Properties

        public IEnumerable<SearchDirection> SearchDirections
        {
            get
            {
                return Enum.GetValues(typeof(SearchDirection))
                    .Cast<SearchDirection>(); 
            }
        }
        public IEditor Editor { get; set; }

        // TODO add tab index
        public bool SearchUp
        {
            get { return _searchUp; }
            set {
                _searchUp = value;
                NotifyOfPropertyChange(() => SearchUp);
            }
        }

        public bool UseWildcards
        {
            get => _useWildcards;
            set
            {
                _useWildcards = value;
                NotifyOfPropertyChange(() => UseWildcards);
            }
        }

        public string TextToFind
        {
            get => _textToFind;
            set
            {
                _textToFind = value;
                NotifyOfPropertyChange(() => TextToFind);
                NotifyOfPropertyChange(() => CanFind);
                if (!UseRegex)
                {
                    // don't try to search as the user typing if we are using regex
                    // as the pattern may not be valid yet
                    FindNext(false); 
                }
            }
        }

        public string TextToReplace
        {
            get => _textToReplace;
            set
            {
                _textToReplace = value;
                NotifyOfPropertyChange(() => TextToReplace);
                NotifyOfPropertyChange(() => CanReplace);
                NotifyOfPropertyChange(() => CanReplaceAll);
            }
        }

        public bool CaseSensitive { get => _caseSensitive;
            set
            {
                _caseSensitive = value;
                NotifyOfPropertyChange(() => CaseSensitive);
            }
        }

        public bool UseRegex { get => _useRegex;
            set { _useRegex = value;
            NotifyOfPropertyChange(() => UseRegex);
            }
        }

        public bool UseWholeWord { get => _useWholeWord;
            set
            {
                _useWholeWord = value;
                NotifyOfPropertyChange(() => UseWholeWord);
            } 
        }

        public bool CanFind => !string.IsNullOrEmpty(TextToFind);
        public bool CanReplace => !string.IsNullOrEmpty(TextToReplace);
        public bool CanReplaceAll => !string.IsNullOrEmpty(TextToReplace);

        private bool _isVisible;
        public bool IsVisible
        {
            get => _isVisible;
            set {
                try
                {
                    _isVisible = value;
                    NotifyOfPropertyChange(() => IsVisible);
                    SelectionActive = false;
                }
                catch(Exception ex)
                {
                    Log.Error(ex, Constants.LogMessageTemplate, nameof(FindReplaceDialogViewModel), nameof(IsVisible), $"Error setting IsVisible: {ex.Message}");
                }
            }
        }

        private bool _showReplace;      

        public bool ShowReplace
        {
            get => _showReplace;
            set { _showReplace = value;
                NotifyOfPropertyChange(nameof(ShowReplace));
                NotifyOfPropertyChange(nameof(TextToReplace));
            }
        }
        public void ToggleReplace()
        {
            ShowReplace = !ShowReplace;
        }

#endregion

#region Methods
        
        public void FindText(bool moveSelection = true)
        {
            try
            {
                if (Editor == null || string.IsNullOrEmpty(TextToFind))
                {
                    SystemSounds.Beep.Play();
                    return;
                }
                if (!FindNextInternal(moveSelection))
                    SystemSounds.Beep.Play();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, "FindReplaceDialogViewModel", "FindText", ex.Message);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error trying to find text: ${ex.Message}"));
            }
        }

        public void FindNext()
        {
            FindNext(true);
        }
        public void FindNext(bool moveSelection = true)
        {
            SearchUp = false;
            FindText(moveSelection);
        }

        public void FindPrev()
        {
            SearchUp = true;
            FindText();
        }

        public void Close()
        {
            IsVisible = false;
        }

        public bool SelectionActive
        {
            get { return _selectionActive; }
            set
            {
                if (Editor == null) return;
                _selectionStart = Editor.SelectionStart;
                _selectionLength = Editor.SelectionLength;
                _selectionActive = value;

                if (_selectionActive)
                {
                    Editor.FindSelectionOffset = _selectionStart;
                    Editor.FindSelectionLength = _selectionLength;
                }
                else
                {
                    Editor.ClearFindSelection();
                }

                NotifyOfPropertyChange(() => SelectionActive);
            }
        }

        public void ReplaceText()
        {
            try
            {
                Regex regex = GetRegEx(TextToFind);
                string input = Editor.Text.Substring(Editor.SelectionStart, Editor.SelectionLength);
                Match match = regex.Match(input);
                bool replaced = false;
                if (match.Success && match.Index == 0 && match.Length == input.Length)
                {
                    Editor.DocumentReplace(Editor.SelectionStart, Editor.SelectionLength, TextToReplace);
                    var delta = TextToReplace.Length - match.Length;
                    if (SelectionActive) _selectionLength += delta;

                    replaced = true;
                }

                if (!FindNextInternal() && !replaced)
                    SystemSounds.Beep.Play();
            }
            catch (Exception ex)
            {
                Log.Error(ex,Constants.LogMessageTemplate,"FindReplaceDialogViewModel","ReplaceText",ex.Message);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error trying to replace text: ${ex.Message}"));
            }
        }

        public void ReplaceAllText()
        {
            try
            {
                Regex regex = GetRegEx(TextToFind, true);
                Editor.BeginChange();
                var initialText = SelectionActive ? Editor.DocumentGetText(_selectionStart, _selectionLength) : Editor.Text;

                var newText = regex.Replace(initialText, TextToReplace);
                
                Editor.DocumentReplace(StartOfSearchScope, initialText.Length, newText);

                Editor.EndChange();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(FindReplaceDialogViewModel), nameof(ReplaceAllText), ex.Message);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error Replacing All Text: {ex.Message}" ));
            }

        }

        private int _selectionStart = 0;
        private int _selectionLength = 0;
        private bool _selectionActive = false;

        private bool FindNextInternal(bool moveSelection = true)
        {
            // TODO - if we have a multi-line selection then we only want to search inside that

            Regex regex = GetRegEx(TextToFind);
            int start = Editor.SelectionStart;
            if (moveSelection)
                 start = regex.Options.HasFlag(RegexOptions.RightToLeft) ? Editor.SelectionStart : Editor.SelectionStart + Editor.SelectionLength;
            Match match = regex.Match(Editor.Text, start);

            if (!match.Success)  // start again from beginning or end
            {
                if (regex.Options.HasFlag(RegexOptions.RightToLeft))
                    match = regex.Match(Editor.Text, EndOfSearchScope);
                else
                    match = regex.Match(Editor.Text, StartOfSearchScope);
            }
            // if we are searching inside the selection and the next match is outside the selection then start again from the beginning or end
            if (match.Success && SelectionActive && (match.Index > EndOfSearchScope || match.Index < StartOfSearchScope))
            {
                if (regex.Options.HasFlag(RegexOptions.RightToLeft))
                    match = regex.Match(Editor.Text, EndOfSearchScope);
                else
                    match = regex.Match(Editor.Text, StartOfSearchScope);
            }

            if ((match.Success && !SelectionActive)
                ||  (SelectionActive  && (match.Index <= EndOfSearchScope && match.Index >= StartOfSearchScope)))
            {
                Editor.Select(match.Index, match.Length);
                TextLocation loc = Editor.DocumentGetLocation(match.Index);
                Editor.ScrollTo(loc.Line, loc.Column);
                return true;
            }

            return false;
        }

        private int EndOfSearchScope { get { return SelectionActive? _selectionStart + _selectionLength : Editor.Text.Length; } }
        private int StartOfSearchScope { get { return SelectionActive ? _selectionStart : 0; } }

        private Regex GetRegEx(string textToFind, bool leftToRight = false)
        {
            RegexOptions options = RegexOptions.None;
            if (SearchUp  && !leftToRight)
                options |= RegexOptions.RightToLeft;
            if (!CaseSensitive )
                options |= RegexOptions.IgnoreCase;

            if (UseRegex )
            {
                return new Regex(textToFind, options);
            }
            else
            {
                string pattern = Regex.Escape(textToFind);
                if (UseWildcards && !UseWholeWord)
                    pattern = pattern.Replace("\\*", ".*").Replace("\\?", ".");
                if (UseWildcards && UseWholeWord)
                    pattern = pattern.Replace("\\*", "[^\\s]*").Replace("\\?", ".");
                if (UseWholeWord)
                    pattern = "\\b" + pattern + "\\b";
                return new Regex(pattern, options);
            }
        }

#endregion
        
    }
}
