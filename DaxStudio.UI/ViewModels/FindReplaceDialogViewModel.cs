using Caliburn.Micro;
using ICSharpCode.AvalonEdit.Document;
using DAXEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DaxStudio.UI.Views;

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
        private static bool _caseSensitive = false;
        private static bool _useRegex = false;
        private static bool _useWildcards = false;
        private static bool _searchUp = false;
        private static bool _useWholeWord;
        private IEditor editor;

        public FindReplaceDialogViewModel(IEditor editor)
        {            
            this.editor = editor;
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
                    .Cast<SearchDirection>(); ;
            }
        }
        public IEditor Editor { get { return editor; } set { editor = value; } }

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
            get { return _useWildcards; }
            set
            {
                _useWildcards = value;
                NotifyOfPropertyChange(() => UseWildcards);
            }
        }

        public string TextToFind
        {
            get { return _textToFind; }
            set
            {
                _textToFind = value;
                NotifyOfPropertyChange(() => TextToFind);
                NotifyOfPropertyChange(() => CanFind);
            }
        }

        public string TextToReplace
        {
            get { return _textToReplace; }
            set
            {
                _textToReplace = value;
                NotifyOfPropertyChange(() => TextToReplace);
                NotifyOfPropertyChange(() => CanReplace);
                NotifyOfPropertyChange(() => CanReplaceAll);
            }
        }

        public bool CaseSensitive { get {return _caseSensitive;}
            set
            {
                _caseSensitive = value;
                NotifyOfPropertyChange(() => CaseSensitive);
            }
        }

        public bool UseRegex { get {return _useRegex;}
            set { _useRegex = value;
            NotifyOfPropertyChange(() => UseRegex);
            }
        }

        public bool UseWholeWord { get {return _useWholeWord;}
            set
            {
                _useWholeWord = value;
                NotifyOfPropertyChange(() => UseWholeWord);
            } 
        }

        public bool CanFind { get { return !string.IsNullOrEmpty(TextToFind); } }
        public bool CanReplace { get { return !string.IsNullOrEmpty(TextToReplace); } }
        public bool CanReplaceAll { get { return !string.IsNullOrEmpty(TextToReplace); } }

        private bool _isVisible;
        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value;
                
            NotifyOfPropertyChange(() => IsVisible);
           //     if (value == true)
           //     {
           //         FocusOnFindBox();
           //     }
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
            get { return _showReplace; }
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
            if (editor == null || string.IsNullOrEmpty( TextToFind))
            {
                SystemSounds.Beep.Play();
                return;
            }
            if (!FindNextInternal())
                SystemSounds.Beep.Play();
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
            Regex regex = GetRegEx(TextToFind);
            string input = editor.Text.Substring(editor.SelectionStart, editor.SelectionLength);
            Match match = regex.Match(input);
            bool replaced = false;
            if (match.Success && match.Index == 0 && match.Length == input.Length)
            {
                editor.DocumentReplace(editor.SelectionStart, editor.SelectionLength, TextToReplace);
                replaced = true;
            }

            if (!FindNextInternal() && !replaced)
                SystemSounds.Beep.Play();
        }

        public void ReplaceAllText()
        {
            // TODO - do we need a dialog??
            //if (MessageBox.Show("Are you sure you want to Replace All occurences of \"" + 
            //TextToFind + "\" with \"" + txtReplace.Text + "\"?",
            //    "Replace All", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                Regex regex = GetRegEx(TextToFind, true);
                int offset = 0;
                editor.BeginChange();
                foreach (Match match in regex.Matches(editor.Text))
                {
                    editor.DocumentReplace(offset + match.Index, match.Length, TextToReplace);
                    offset += TextToReplace.Length - match.Length;
                }
                editor.EndChange();
            }
        }

        private bool FindNextInternal()
        {
            Regex regex = GetRegEx(TextToFind);
            int start = regex.Options.HasFlag(RegexOptions.RightToLeft) ? 
            editor.SelectionStart : editor.SelectionStart + editor.SelectionLength;
            Match match = regex.Match(editor.Text, start);

            if (!match.Success)  // start again from beginning or end
            {
                if (regex.Options.HasFlag(RegexOptions.RightToLeft))
                    match = regex.Match(editor.Text, editor.Text.Length);
                else
                    match = regex.Match(editor.Text, 0);
            }

            if (match.Success)
            {
                editor.Select(match.Index, match.Length);
                TextLocation loc = editor.DocumentGetLocation(match.Index);
                editor.ScrollTo(loc.Line, loc.Column);
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
                if (UseWildcards)
                    pattern = pattern.Replace("\\*", ".*").Replace("\\?", ".");
                if (UseWholeWord)
                    pattern = "\\b" + pattern + "\\b";
                return new Regex(pattern, options);
            }
        }

#endregion



        
    }
}
