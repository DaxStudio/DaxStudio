namespace UnitComboLib.Behaviour
{
    using System;
    using System.Windows;
  using System.Windows.Controls;

  /// <summary>
  /// This class can be used to populate a context-menu
  /// when a user clicks on a <seealso cref="FrameworkElement"/>.
  /// </summary>
  public static class ContextMenuBehaviour
  {
    #region fields
    private static readonly DependencyProperty MenuListProperty =
        DependencyProperty.RegisterAttached("MenuList",
                                            typeof(ContextMenu),
                                            typeof(ContextMenuBehaviour),
                                            new UIPropertyMetadata(null, ContextMenuBehaviour.OnMenuListChanged));
    #endregion fields

    #region methods
    /// <summary>
    /// Implements the get portion of the <seealso cref="ContextMenu"/> dependency property.
    /// </summary>
    /// <param name="contextMenu"></param>
    /// <returns></returns>
    public static ContextMenu GetMenuList(DependencyObject contextMenu)
    {
            if (contextMenu == null) throw new ArgumentNullException(nameof(contextMenu));
            return (ContextMenu)contextMenu.GetValue(MenuListProperty);
    }

    /// <summary>
    /// IMplements the set portion of the <seealso cref="ContextMenu"/> dependency property.
    /// </summary>
    /// <param name="contextMenu"></param>
    /// <param name="value"></param>
    public static void SetMenuList(DependencyObject contextMenu, ContextMenu value)
    {
            if (contextMenu is null)
            {
                throw new ArgumentNullException(nameof(contextMenu));
            }

            contextMenu.SetValue(MenuListProperty, value);
    }

    /// <summary>
    /// This method is fired when the <seealso cref="ContextMenu"/> dependency property is set or reset.
    /// </summary>
    /// <param name="d"></param>
    /// <param name="e"></param>
    private static void OnMenuListChanged(DependencyObject d,
                                        DependencyPropertyChangedEventArgs e)
    {
      var element = d as FrameworkElement;

      if (element != null)
      {
        element.MouseLeftButtonUp += Element_MouseLeftButtonUp;
      }
      else
        element.MouseLeftButtonUp -= Element_MouseLeftButtonUp;
    }

    /// <summary>
    /// This method is fired when the user clicks on the attached <seealso cref="FrameworkElement"/>.
    /// Its goal is to either open the corresponding <seealso cref="ContextMenu"/> via the <seealso cref="ContextMenu"/> dependency property
    /// or attempt to open the standard context menu of the attached <seealso cref="FrameworkElement"/> if the dependency property is not set.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void Element_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      var element = sender as FrameworkElement;

      var target = GetMenuList(element);

      if (target != null)
      {
        // Open context menu defined through dependency property
        target.PlacementTarget = (UIElement)sender;
        target.IsOpen = true;
      }
      else
      {
        if (element != null)
        {
          // Open context menu on attached framework element
          element.ContextMenu.PlacementTarget = (UIElement)sender;
          element.ContextMenu.IsOpen = true;
        }
      }
    }
    #endregion methods
  }
}
