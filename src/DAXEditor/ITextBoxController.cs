namespace DAXEditorControl
{
  /// <summary>
  /// Define a deligate method that is called for processing the SelectAll event.
  /// </summary>
  /// <param name="sender"></param>
  public delegate void SelectAllEventHandler(ITextBoxController sender);

  /// <summary>
  /// Define a deligate method that is called for processing the Select event.
  /// </summary>
  /// <param name="sender"></param>
  /// <param name="start"></param>
  /// <param name="lenght"></param>
  public delegate void SelectEventHandler(ITextBoxController sender, int start, int lenght);

  public delegate void ScrollToLineEventHandler(ITextBoxController sender, int line);

  public delegate void CurrentSelectionEventHandler(ITextBoxController sender,
                                                    out int start, out int length,
                                                    out bool IsRectengularSelection);

  public delegate void BeginChangeEventHandler(ITextBoxController sender);

  public delegate void EndChangeEventHandler(ITextBoxController sender);

  public delegate void GetSelectedTextEventHandler(ITextBoxController sender, out string selectedText);

  /// <summary>
  /// Define an interface that must be adhered to when establishing
  /// comunication between ViewModel <seealso cref="MyViewModel"/>
  /// and attached property <seealso cref="AvalonEditAttach"/>.
  /// </summary>
  public interface ITextBoxController
  {
    event SelectAllEventHandler SelectAllEvent;

    event SelectEventHandler SelectEvent;

    event ScrollToLineEventHandler ScrollToLineEvent;

    event CurrentSelectionEventHandler CurrentSelectionEvent;

    event BeginChangeEventHandler BeginChangeEvent;

    event EndChangeEventHandler EndChangeEvent;

    event GetSelectedTextEventHandler GetSelectedTextEvent;
  }
}
