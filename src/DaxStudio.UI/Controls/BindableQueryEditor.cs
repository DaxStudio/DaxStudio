using DaxStudio.Common;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using ICSharpCode.AvalonEdit;
using System.Windows;

namespace DaxStudio.UI.Controls
{
    /// <summary>
    /// A read-only AvalonEdit TextEditor that supports data binding for Text and
    /// automatically selects the appropriate syntax highlighting (xmSQL or DirectQuery SQL).
    /// </summary>
    public class BindableQueryEditor : TextEditor
    {
        public BindableQueryEditor()
        {
            IsReadOnly = true;
            ShowLineNumbers = true;
            WordWrap = true;
            Options.EnableHyperlinks = false;
            Options.EnableEmailHyperlinks = false;
        }

        #region BoundText DependencyProperty

        public static readonly DependencyProperty BoundTextProperty =
            DependencyProperty.Register(
                nameof(BoundText),
                typeof(string),
                typeof(BindableQueryEditor),
                new FrameworkPropertyMetadata(null, OnBoundTextChanged));

        public string BoundText
        {
            get => (string)GetValue(BoundTextProperty);
            set => SetValue(BoundTextProperty, value);
        }

        private static void OnBoundTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (BindableQueryEditor)d;
            var newText = e.NewValue as string ?? string.Empty;

            // Break very long lines to prevent AvalonEdit colorization freezes.
            // This uses the same LongLineStateMachine approach as the paste handler
            // in DocumentViewModel.
            if (newText.Length > 0 && HasLongLines(newText))
            {
                var sm = new LongLineStateMachine(Constants.MaxLineLength);
                newText = sm.ProcessString(newText);
            }

            editor.Text = newText;
        }

        /// <summary>
        /// Quick check for whether any line exceeds the max length threshold.
        /// Avoids the overhead of LongLineStateMachine for normal-sized text.
        /// </summary>
        private static bool HasLongLines(string text)
        {
            int lineLength = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                    lineLength = 0;
                else
                    lineLength++;

                if (lineLength > Constants.MaxLineLength)
                    return true;
            }
            return false;
        }

        #endregion

        #region QueryLanguage DependencyProperty

        public static readonly DependencyProperty QueryLanguageProperty =
            DependencyProperty.Register(
                nameof(QueryLanguage),
                typeof(DaxStudioTraceEventClassSubclass.Language),
                typeof(BindableQueryEditor),
                new FrameworkPropertyMetadata(
                    DaxStudioTraceEventClassSubclass.Language.Unknown,
                    OnQueryLanguageChanged));

        public DaxStudioTraceEventClassSubclass.Language QueryLanguage
        {
            get => (DaxStudioTraceEventClassSubclass.Language)GetValue(QueryLanguageProperty);
            set => SetValue(QueryLanguageProperty, value);
        }

        private static void OnQueryLanguageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (BindableQueryEditor)d;
            var language = (DaxStudioTraceEventClassSubclass.Language)e.NewValue;
            editor.UpdateSyntaxHighlighting(language);
        }

        #endregion

        private void UpdateSyntaxHighlighting(DaxStudioTraceEventClassSubclass.Language language)
        {
            switch (language)
            {
                case DaxStudioTraceEventClassSubclass.Language.SQL:
                    SyntaxHighlighting = SyntaxHighlightingHelper.DirectQuerySqlHighlighting;
                    break;
                default:
                    SyntaxHighlighting = SyntaxHighlightingHelper.XmSqlHighlighting;
                    break;
            }
        }
    }
}
