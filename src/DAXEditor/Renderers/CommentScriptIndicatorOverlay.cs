using System;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace DAXEditorControl.Renderers
{
    public sealed class CommentScriptIndicatorOverlay : FrameworkElement
    {
        private const double IndicatorGap = 2.0;
        private DAXEditor _subscribedEditor;

        public static readonly DependencyProperty EditorProperty =
            DependencyProperty.Register(
                nameof(Editor),
                typeof(DAXEditor),
                typeof(CommentScriptIndicatorOverlay),
                new FrameworkPropertyMetadata(null, OnEditorChanged));

        public CommentScriptIndicatorOverlay()
        {
            IsHitTestVisible = false;
            ClipToBounds = false;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public DAXEditor Editor
        {
            get => (DAXEditor)GetValue(EditorProperty);
            set => SetValue(EditorProperty, value);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var editor = Editor;
            var textView = editor?.TextArea?.TextView;
            var document = editor?.Document;
            if (textView == null || document == null || !textView.VisualLinesValid) return;
            if (PresentationSource.FromVisual(textView) == null || PresentationSource.FromVisual(this) == null) return;

            var textLeft = textView.TranslatePoint(new Point(0, 0), this).X;

            foreach (var visualLine in textView.VisualLines)
            {
                var documentLine = visualLine.FirstDocumentLine;
                var text = document.GetText(documentLine.Offset, documentLine.Length);
                if (!CommentScriptCommandStyle.TryClassify(text, out var style)) continue;

                var brush = GetIndicatorBrush(editor, style.CommandColorName);
                if (brush == null) continue;

                var segment = new TextSegment
                {
                    StartOffset = documentLine.Offset,
                    EndOffset = documentLine.EndOffset
                };
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    var topLeft = textView.TranslatePoint(new Point(0, rect.Top), this);
                    drawingContext.DrawRectangle(
                        brush,
                        null,
                        new Rect(
                            textLeft - IndicatorGap - style.IndicatorWidth,
                            topLeft.Y,
                            style.IndicatorWidth,
                            rect.Height));
                }
            }
        }

        private static Brush GetIndicatorBrush(DAXEditor editor, string colorName)
        {
            var color = editor.SyntaxHighlighting?.GetNamedColor(colorName);
            return color?.Foreground?.GetBrush(null);
        }

        private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var overlay = (CommentScriptIndicatorOverlay)d;
            overlay.Unsubscribe();
            if (overlay.IsLoaded)
                overlay.Subscribe(e.NewValue as DAXEditor);
            overlay.InvalidateVisual();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Subscribe(Editor);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unsubscribe();
        }

        private void Subscribe(DAXEditor editor)
        {
            if (editor == null || ReferenceEquals(editor, _subscribedEditor)) return;
            Unsubscribe();
            _subscribedEditor = editor;
            editor.TextArea.TextView.VisualLinesChanged += TextViewOnVisualLinesChanged;
            editor.CommentScriptStylesChanged += EditorOnCommentScriptStylesChanged;
        }

        private void Unsubscribe()
        {
            if (_subscribedEditor == null) return;
            _subscribedEditor.TextArea.TextView.VisualLinesChanged -= TextViewOnVisualLinesChanged;
            _subscribedEditor.CommentScriptStylesChanged -= EditorOnCommentScriptStylesChanged;
            _subscribedEditor = null;
        }

        private void TextViewOnVisualLinesChanged(object sender, EventArgs e)
        {
            InvalidateVisual();
        }

        private void EditorOnCommentScriptStylesChanged(object sender, EventArgs e)
        {
            InvalidateVisual();
        }
    }
}
