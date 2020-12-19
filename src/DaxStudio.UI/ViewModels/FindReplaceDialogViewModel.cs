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

namespace DaxStudio.UI.ViewModels
{
    public enum SearchDirection
    {
        Next,
        Prev
    }
        
    public class FindReplaceDialogViewModel : Screen
    {

        // TODO - fix the following sample code to fit MVVM

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
            //this.editor = editor;
            //_searchDirections = new List<string>();
            //_searchDirections.Add("Next");
            //_searchDirections.Add("Prev");
        }

#region Properties
        //private List<string> _searchDirections;
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
                    if (value)
                    {
                        //FocusOnFindBox();
                        this.SetFocus(() => TextToFind);
                    }
                }
                catch(Exception ex)
                {
                    Log.Error(ex, Constants.LogMessageTemplate, nameof(FindReplaceDialogViewModel), nameof(IsVisible), $"Error setting IsVisible: {ex.Message}");
                }
            }
        }

        //private void FocusOnFindBox()
        //{
        //    var v = (FindReplaceDialogView)this.GetView();
        //    v.FocusOnFind();
        //}

        private bool _showReplace;
        

        public bool ShowReplace
        {
            get => _showReplace;
            set { _showReplace = value;
                NotifyOfPropertyChange(() => ShowReplace);
            }
        }
        public void ToggleReplace()
        {
            ShowReplace = !ShowReplace;
        }

#endregion


#region Methods
        
        public void FindText()
        {
            try
            {
                if (Editor == null || string.IsNullOrEmpty(TextToFind))
                {
                    SystemSounds.Beep.Play();
                    return;
                }
                if (!FindNextInternal())
                    SystemSounds.Beep.Play();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, "FindReplaceDialogViewModel", "FindText", ex.Message);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error trying to find text: ${ex.Message}"));
            }
        }

        public void FindNext()
        {
            SearchUp = false;
            FindText();
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
                    replaced = true;
                }

                if (!FindNextInternal() && !replaced)
                    SystemSounds.Beep.Play();
            }
            catch (Exception ex)
            {
                Log.Error(ex,Constants.LogMessageTemplate,"FindReplaceDialogViewModel","ReplaceText",ex.Message);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error trying to replace text: ${ex.Message}"));
            }
        }

        public void ReplaceAllText()
        {
            try
            {
                Regex regex = GetRegEx(TextToFind, true);
                int offset = 0;
                Editor.BeginChange();
                // TODO  if selectionlength > 0 replace only in selection
                foreach (Match match in regex.Matches(Editor.Text))
                {
                    Editor.DocumentReplace(offset + match.Index, match.Length, TextToReplace);
                    offset += TextToReplace.Length - match.Length;
                }

                Editor.EndChange();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(FindReplaceDialogViewModel), nameof(ReplaceAllText), ex.Message);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error Replacing All Text: {ex.Message}" ));
            }

        }

        private bool FindNextInternal()
        {
            // TODO - if we have a multi-line selection then we only want to search inside that

            Regex regex = GetRegEx(TextToFind);
            int start = regex.Options.HasFlag(RegexOptions.RightToLeft) ? 
            Editor.SelectionStart : Editor.SelectionStart + Editor.SelectionLength;
            Match match = regex.Match(Editor.Text, start);

            if (!match.Success)  // start again from beginning or end
            {
                if (regex.Options.HasFlag(RegexOptions.RightToLeft))
                    match = regex.Match(Editor.Text, Editor.Text.Length);
                else
                    match = regex.Match(Editor.Text, 0);
            }

            if (match.Success)
            {
                Editor.Select(match.Index, match.Length);
                TextLocation loc = Editor.DocumentGetLocation(match.Index);
                Editor.ScrollTo(loc.Line, loc.Column);
            }

            return match.Success;
        }
        
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
