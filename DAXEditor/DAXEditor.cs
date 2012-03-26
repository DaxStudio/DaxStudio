using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit;
using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.CodeCompletion;

namespace DAXEditor
{
    /// <summary>
    /// Follow steps 1a or 1b and then 2 to use this custom control in a XAML file.
    ///
    /// Step 1a) Using this custom control in a XAML file that exists in the current project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:DAXEditor"
    ///
    ///
    /// Step 1b) Using this custom control in a XAML file that exists in a different project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:DAXEditor;assembly=DAXEditor"
    ///
    /// You will also need to add a project reference from the project where the XAML file lives
    /// to this project and Rebuild to avoid compilation errors:
    ///
    ///     Right click on the target project in the Solution Explorer and
    ///     "Add Reference"->"Projects"->[Select this project]
    ///
    ///
    /// Step 2)
    /// Go ahead and use your control in the XAML file.
    ///
    ///     <MyNamespace:CustomControl1/>
    ///
    /// </summary>
    public class DAXEditor : ICSharpCode.AvalonEdit.TextEditor
    {
        static DAXEditor()
        {
            //DefaultStyleKeyProperty.OverrideMetadata(typeof(DAXEditor), new FrameworkPropertyMetadata(typeof(DAXEditor)));

        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            this.TextArea.TextEntering += textEditor_TextArea_TextEntering;
            this.TextArea.TextEntered += textEditor_TextArea_TextEntered;
            System.Reflection.Assembly myAssembly = System.Reflection.Assembly.GetAssembly(this.GetType());
            using (Stream s = myAssembly.GetManifestResourceStream("DAXEditor.Resources.DAX.xshd"))
            {
                using (XmlTextReader reader = new XmlTextReader(s))
                {
                    this.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
            //ToolTip tt = new ToolTip();
            //tt.Content = "hello";


            //this.textEditor1.ToolTip = tt;
        }

        CompletionWindow completionWindow;

        void textEditor_TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            /*  TODO - temporarily commented out - we need a DAX parser before we can 
             *         properly implement intellisense like functionality
             *         
            if (e.Text == "(" || e.Text == " ")
            {
                
                // Open code completion after the user has pressed dot:
                completionWindow = new CompletionWindow(this.TextArea);

                // TODO - load completion data from metadata
                IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
                data.Add(new MyCompletionData("Calculate", CodeCompletionType.Function));
                data.Add(new MyCompletionData("CalculateTable", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Earlier", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Earliest", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Filter", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Max", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Min", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Related", CodeCompletionType.Function));
                data.Add(new MyCompletionData("RelatedTable", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Values", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Summarize", CodeCompletionType.Function));

                completionWindow.Show();
                completionWindow.Closed += delegate
                {
                    completionWindow = null;
                };
            }
             * */
        }

        void textEditor_TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]))
                {
                    // Whenever a non-letter is typed while the completion window is open,
                    // insert the currently selected element.
                    completionWindow.CompletionList.RequestInsertion(e);
                }
            }
            // Do not set e.Handled=true.
            // We still want to insert the character that was typed.
        }
    }
}
