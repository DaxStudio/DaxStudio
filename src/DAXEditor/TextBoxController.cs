namespace DAXEditorControl
{
    public class TextBoxController : ITextBoxController
  {
    #region Events
    public event SelectAllEventHandler SelectAllEvent;

    public event SelectEventHandler SelectEvent;

    public event ScrollToLineEventHandler ScrollToLineEvent;

    public event CurrentSelectionEventHandler CurrentSelectionEvent;

    public event BeginChangeEventHandler BeginChangeEvent;

    public event EndChangeEventHandler EndChangeEvent;

    public event GetSelectedTextEventHandler GetSelectedTextEvent;
    #endregion Events

    #region methods
    public void SelectAllText()
    {
      if (this.SelectAllEvent != null)
        this.SelectAllEvent(this);
    }

    public void SelectText(int start, int length)
    {
      if (this.SelectEvent != null)
      {
        this.SelectEvent(this, start, length);       // Execute select event to be propagated into view via attached property
      }
    }

    public void ScrollToLine(int line)
    {
      if (this.ScrollToLineEvent != null)
        this.ScrollToLineEvent(this, line);       // Execute select event to be propagated into view via attached property
    }

    public void CurrentSelection(out int start, out int length, out bool IsRectengularSelection)
    {
      start = length = 0;
      IsRectengularSelection = false;

      // Execute select event to be propagated into view via attached property
      if (this.CurrentSelectionEvent != null)
        this.CurrentSelectionEvent(this, out start, out length, out IsRectengularSelection);
    }

    public void BeginChange()
    {
      if (this.BeginChangeEvent != null)
        this.BeginChangeEvent(this);
    }

    public void EndChange()
    {
      if (this.EndChangeEvent != null)
        this.EndChangeEvent(this);
    }

    public void GetSelectedText(out string selectedText)
    {
      selectedText = string.Empty;

      if (this.GetSelectedTextEvent != null)
        this.GetSelectedTextEvent(this, out selectedText);
    }
    #endregion methods
  }
}
