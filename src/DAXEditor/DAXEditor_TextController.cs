using System;
using System.Collections.Generic;
using System.Windows;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace DAXEditorControl
{
    public partial class DAXEditor : TextEditor
  {
    #region ITextBoxControllerFields
    private static readonly Dictionary<ITextBoxController, TextEditor> elements = new Dictionary<ITextBoxController, TextEditor>();

    private static readonly DependencyProperty TextBoxControllerProperty =
                            DependencyProperty.Register(
                              "TextBoxController",
                              typeof(ITextBoxController),
                              typeof(DAXEditor),
                              new FrameworkPropertyMetadata(null, DAXEditor.OnTextBoxControllerChanged));
    #endregion ITextBoxControllerFields

    #region ITextBoxController_Properties
    internal static void SetTextBoxController(UIElement element, ITextBoxController value)
    {
      element.SetValue(DAXEditor.TextBoxControllerProperty, value);
    }

    internal static ITextBoxController GetTextBoxController(UIElement element)
    {
      return (ITextBoxController)element.GetValue(DAXEditor.TextBoxControllerProperty);
    }
    #endregion ITextBoxController_Properties

    #region ITextBoxController_methods
    /// <summary>
    /// Call corresponding on changed method when the depependency property
    /// for this <seealso cref="ITextBoxController"/> is changed.
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private static void OnTextBoxControllerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      if (!(d is DAXEditor fileDoc))
        return;
            ////throw new ArgumentNullException("Object of type FileDocument is not available!");

            var txtBox = fileDoc as DAXEditor;

            // Remove event handler from old if OldValue is available
            if (e.OldValue is ITextBoxController oldController)
            {
                elements.Remove(oldController);
                oldController.SelectAllEvent -= SelectAll;
                oldController.SelectEvent -= Select;
                oldController.ScrollToLineEvent -= ScrollToLine;
                oldController.CurrentSelectionEvent -= CurrentSelection;
                oldController.BeginChangeEvent -= BeginChange;
                oldController.EndChangeEvent -= EndChange;
                oldController.GetSelectedTextEvent -= GetSelectedText;
            }

            // Add new eventhandler for each event declared in the interface declaration
            if (e.NewValue is ITextBoxController newController)
            {
                // Sometime the newController is already there but the event handling is not working
                // Remove controller and event handling and install a new one instead.
                if (elements.TryGetValue(newController, out TextEditor test))
                {
                    elements.Remove(newController);

                    newController.SelectAllEvent -= DAXEditor.SelectAll;
                    newController.SelectEvent -= DAXEditor.Select;
                    newController.ScrollToLineEvent -= DAXEditor.ScrollToLine;
                    newController.CurrentSelectionEvent -= DAXEditor.CurrentSelection;
                    newController.BeginChangeEvent -= DAXEditor.BeginChange;
                    newController.EndChangeEvent -= DAXEditor.EndChange;
                    newController.GetSelectedTextEvent -= DAXEditor.GetSelectedText;
                }

                elements.Add(newController, txtBox);
                newController.SelectAllEvent += SelectAll;
                newController.SelectEvent += Select;
                newController.ScrollToLineEvent += ScrollToLine;
                newController.CurrentSelectionEvent += CurrentSelection;
                newController.BeginChangeEvent += DAXEditor.BeginChange;
                newController.EndChangeEvent += DAXEditor.EndChange;
                newController.GetSelectedTextEvent += DAXEditor.GetSelectedText;
            }
        }

    /// <summary>
    /// Select all text in the editor
    /// </summary>
    /// <param name="sender"></param>
    private static void SelectAll(ITextBoxController sender)
    {
            if (!elements.TryGetValue(sender, out TextEditor element))
                throw new ArgumentException("Unable to get element from sender for SelecteAll method");

            element.Focus();
      element.SelectAll();
    }

    /// <summary>
    /// Select the text in the editor as indicated by <paramref name="start"/>
    /// and <paramref name="length"/>.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="start"></param>
    /// <param name="length"></param>
    private static void Select(ITextBoxController sender, int start, int length)
    {
       if (!elements.TryGetValue(sender, out TextEditor element))
                throw new ArgumentException("Unable to get element from sender for Select method");

            // element.Focus();

      element.Select(start, length);
      TextLocation loc = element.Document.GetLocation(start);
      element.ScrollTo(loc.Line, loc.Column);
    }

    /// <summary>
    /// Scroll to a specific line in the currently displayed editor text
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="line"></param>
    private static void ScrollToLine(ITextBoxController sender, int line)
    {
      if (!elements.TryGetValue(sender, out TextEditor element))
                throw new ArgumentException("Unable to get element from sender for ScrollToline method");

      element.Focus();
      element.ScrollToLine(line);
    }

    /// <summary>
    /// Get the state of the current selection start, end and whether its rectangular or not.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="start"></param>
    /// <param name="length"></param>
    /// <param name="IsRectangularSelection"></param>
    private static void CurrentSelection(ITextBoxController sender,
                                         out int start, out int length, out bool IsRectangularSelection)
    {

      if (!elements.TryGetValue(sender, out TextEditor element))
                throw new ArgumentException("Unable to get Element for sender in CurrentSelection method");

      start = element.SelectionStart;
      length = element.SelectionLength;
      IsRectangularSelection = element.TextArea.Selection.EnableVirtualSpace;

      // element.TextArea.Selection = RectangleSelection.Create(element.TextArea, start, length);
    }

    private static void BeginChange(ITextBoxController sender)
    {

      if (!elements.TryGetValue(sender, out TextEditor element))
                throw new ArgumentException("Unable to get element for sender in BeginChange");

      element.BeginChange();
    }

    private static void EndChange(ITextBoxController sender)
    {

      if (!elements.TryGetValue(sender, out TextEditor element))
                throw new ArgumentException("Unable to get element for sender in EndChange");

      element.EndChange();
    }


    private static void GetSelectedText(ITextBoxController sender, out string selectedText)
    {
      if (!elements.TryGetValue(sender, out TextEditor element))
                throw new ArgumentException("Unable to get element for sender in GetSelectedText");

      selectedText = element.SelectedText;
    }
    #endregion ITextBoxController_methods
  }
}
