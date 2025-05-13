using DaxStudio.UI.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace DaxStudio.UI.AttachedProperties
{
    public class ContentControlLineNumberHelper
    {
        #region BindableLineCount AttachedProperty
        public static string GetBindableLineCount(DependencyObject obj)
        {
            return (string)obj.GetValue(BindableLineCountProperty);
        }

        public static void SetBindableLineCount(DependencyObject obj, string value)
        {
            obj.SetValue(BindableLineCountProperty, value);
        }

        // Using a DependencyProperty as the backing store for BindableLineCount.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BindableLineCountProperty =
            DependencyProperty.RegisterAttached(
            "BindableLineCount",
            typeof(string),
            typeof(ContentControlLineNumberHelper),
            new UIPropertyMetadata("1"));

        #endregion // BindableLineCount AttachedProperty

        #region HasBindableLineCount AttachedProperty
        public static bool GetHasBindableLineCount(DependencyObject obj)
        {
            return (bool)obj.GetValue(HasBindableLineCountProperty);
        }

        public static void SetHasBindableLineCount(DependencyObject obj, bool value)
        {
            obj.SetValue(HasBindableLineCountProperty, value);
        }

        // Using a DependencyProperty as the backing store for HasBindableLineCount.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HasBindableLineCountProperty =
            DependencyProperty.RegisterAttached(
            "HasBindableLineCount",
            typeof(bool),
            typeof(ContentControlLineNumberHelper),
            new UIPropertyMetadata(
                false,
                new PropertyChangedCallback(OnHasBindableLineCountChanged)));

        private static void OnHasBindableLineCountChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var textBox = (RichTextBox)o;
            var lineCount = FlowDocumentHelper.CountLines(textBox.Document as FlowDocument);
            if ((e.NewValue as bool?) == true)
            {
                textBox.SizeChanged += new SizeChangedEventHandler(box_SizeChanged);
                textBox.TextChanged += new TextChangedEventHandler(box_TextChanged);
                textBox.SetValue(BindableLineCountProperty, lineCount.ToString());
            }
            else
            {
                textBox.SizeChanged -= new SizeChangedEventHandler(box_SizeChanged);
                textBox.TextChanged -= new TextChangedEventHandler(box_TextChanged);
            }
        }

        private static void box_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateLineNumbers(sender);
        }

        static void box_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLineNumbers(sender);
        }

        private static void UpdateLineNumbers(object sender)
        {
            var textBox = (Controls.BindableRichTextBox)sender;
            var lineCount = FlowDocumentHelper.CountLines(textBox.Document);
            string x = string.Empty;
            for (int i = 0; i < lineCount; i++)
            {
                x += i + 1 + "\n";
            }
            textBox.SetValue(BindableLineCountProperty, x);
        }
        #endregion // HasBindableLineCount AttachedProperty
    }
}