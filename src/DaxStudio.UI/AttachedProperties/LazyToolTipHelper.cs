using System;
using System.Windows;
using System.Windows.Controls;
using Serilog;

namespace DaxStudio.UI.AttachedProperties
{
    /// <summary>
    /// Attached property to enable lazy loading of tooltips.
    /// When enabled, the tooltip content is only created when the tooltip is about to be displayed,
    /// avoiding performance issues when loading large models with many items.
    /// Usage: Set LazyToolTipTemplate to a DataTemplate, and the tooltip will be created lazily on demand.
    /// </summary>
    public static class LazyToolTipHelper
    {
        // Placeholder object to identify an uninitialized tooltip
        private static readonly object TooltipPlaceholder = new object();

        #region LazyToolTipTemplate Attached Property

        public static readonly DependencyProperty LazyToolTipTemplateProperty =
            DependencyProperty.RegisterAttached(
                "LazyToolTipTemplate",
                typeof(DataTemplate),
                typeof(LazyToolTipHelper),
                new PropertyMetadata(null, OnLazyToolTipTemplateChanged));

        public static DataTemplate GetLazyToolTipTemplate(DependencyObject obj)
        {
            return (DataTemplate)obj.GetValue(LazyToolTipTemplateProperty);
        }

        public static void SetLazyToolTipTemplate(DependencyObject obj, DataTemplate value)
        {
            obj.SetValue(LazyToolTipTemplateProperty, value);
        }

        #endregion

        #region Private Methods

        private static void OnLazyToolTipTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                if (e.NewValue is DataTemplate)
                {
                    // Initially set a placeholder tooltip to enable the ToolTipOpening event
                    // The actual content will be created lazily when the tooltip opens
                    element.ToolTip = TooltipPlaceholder;

                    // Subscribe to the opening event
                    element.ToolTipOpening += OnToolTipOpening;
                    element.ToolTipClosing += OnToolTipClosing;
                }
                else
                {
                    // Unsubscribe from events
                    element.ToolTipOpening -= OnToolTipOpening;
                    element.ToolTipClosing -= OnToolTipClosing;
                }
            }
        }

        private static void OnToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var template = GetLazyToolTipTemplate(element);
                if (template != null)
                {
                    // Check if we have the placeholder (not yet initialized)
                    if (ReferenceEquals(element.ToolTip, TooltipPlaceholder))
                    {
                        try
                        {
                            // Create the tooltip content from the template on demand
                            var content = template.LoadContent() as FrameworkElement;
                            if (content != null)
                            {
                                // Set the DataContext to the element's DataContext
                                content.DataContext = element.DataContext;

                                // Create and set the tooltip
                                var tooltip = new ToolTip
                                {
                                    Content = content
                                };
                                element.ToolTip = tooltip;
                            }
                            else
                            {
                                Log.Warning("LazyToolTipHelper: Template.LoadContent() did not return a FrameworkElement");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "LazyToolTipHelper: Error loading tooltip template content");
                        }
                    }
                }
            }
        }

        private static void OnToolTipClosing(object sender, ToolTipEventArgs e)
        {
            // When the tooltip closes, we reset it to a placeholder
            // This allows the tooltip content to be garbage collected
            // and recreated fresh the next time it opens
            if (sender is FrameworkElement element)
            {
                var template = GetLazyToolTipTemplate(element);
                if (template != null)
                {
                    // Reset to placeholder for next time
                    element.ToolTip = TooltipPlaceholder;
                }
            }
        }

        #endregion
    }
}
