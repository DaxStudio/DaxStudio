using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DaxStudio.CheckerApp
{
    public static class TextBoxExtensions
    {

        public const string INDENT = "    ";
        public static void AppendLine(this System.Windows.Controls.RichTextBox myOutput)
        {
            myOutput.AppendText("\n");
        }
        public static void AppendLine(this System.Windows.Controls.RichTextBox myOutput, string text)
        {
            //myOutput.AppendText(text + "\r");
            //myOutput.Document.Blocks.Add(new Paragraph(new Run(text + "\r")));
            myOutput.AppendLine(text, "Black");
        }

        public static void AppendLine(this System.Windows.Controls.RichTextBox myOutput, string text, string colour)
        {
            //myOutput.AppendText(text + "\r");
            //myOutput.Document.Blocks.Add(new Paragraph(new Run(text + "\r")));
            myOutput.AppendRange(text + "\r").Color(colour);
        }

        public static TextRange AppendIndentedLine(this System.Windows.Controls.RichTextBox myOutput, string text)
        {
            return myOutput.AppendIndentedLine(text, "Black");
        }

        public static TextRange AppendIndentedLine(this System.Windows.Controls.RichTextBox myOutput, string text, string colour)
        {
            return myOutput.AppendRange(INDENT + text + "\r\n").Color(colour);
        }

        //public static TextRange Indent(this System.Windows.Controls.RichTextBox myOutput)
        //{
        //    //myOutput.AppendText(text + "\r");
        //    //myOutput.Document.Blocks.Add(new Paragraph(new Run(text + "\r")));
        //    return  myOutput.AppendRange("   ");
        //}

        


        public static void AppendHeaderLine(this System.Windows.Controls.RichTextBox myOutput, string text)
        {
            myOutput.AppendLine();
            myOutput.AppendRange(text +"\n").Bold().Size("14pt");
            myOutput.AppendLine("=======================");
            //var inline = new Run();

            //myOutput.Document.Blocks.Add( new Paragraph( new Bold( new Underline( new Run( text + "\r")))));
        }

        public static TextRange AppendRange(this System.Windows.Controls.RichTextBox box, string text)
        {
            BrushConverter bc = new BrushConverter();
            TextRange tr = new TextRange(box.Document.ContentEnd, box.Document.ContentEnd);
            tr.Text = text;
            
            try
            {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty,
                    bc.ConvertFromString("Black"));
                tr.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
                tr.ApplyPropertyValue(TextElement.FontSizeProperty, new FontSizeConverter().ConvertFromString("10pt"));
            }
            catch (FormatException) { }

            return tr;
        }

        public static TextRange Color(this TextRange range, string color)
        {
            BrushConverter bc = new BrushConverter();
            range.ApplyPropertyValue(TextElement.ForegroundProperty, bc.ConvertFromString(color));
            return range;
        }

        public static TextRange Bold(this TextRange range)
        {
            range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            return range;
        }

        public static TextRange Size(this TextRange range, string size)
        {
            FontSizeConverter fc = new FontSizeConverter();
            var fontSize = fc.ConvertFromString(size);
            range.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
            return range;
        }

        public static TextRange Indent( this TextRange range, int margin)
        {
            range.ApplyPropertyValue(Paragraph.MarginProperty, new Thickness(margin,0,0,0));
            return range;
        }

        public static TextRange Indent(this TextRange range)
        {
            string[] lines = range.Text.Split('\n');
            lines[lines.Length - 1] = INDENT + lines[lines.Length - 1];
            //range.ApplyPropertyValue(Paragraph.MarginProperty, new Thickness(margin, 0, 0, 0));
            range.Text = string.Join("\n", lines);
            return range;
        }

        //public static TextRange Underline(this TextRange range)
        //{
        //    range.ApplyPropertyValue(TextElement.);
        //    return range;
        //}

    }
}
