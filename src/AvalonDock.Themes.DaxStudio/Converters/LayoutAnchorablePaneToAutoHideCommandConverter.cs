using AvalonDock.Layout;
using AvalonDock.Controls;
using System;
using System.Globalization;
using System.Windows.Data;

namespace AvalonDock.Converters
{
    public class LayoutAnchorablePaneToAutoHideCommandConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is LayoutAnchorablePaneControl paneCtrl)) return null;

            if (!(paneCtrl.Model is LayoutAnchorablePane pane)) return null;

            if (!(pane.SelectedContent is LayoutContent content)) return null;

            if (!(content.Root.Manager.GetLayoutItemFromModel(content) is LayoutAnchorableItem item)) return null;

            string param = parameter as string;
            switch (param)
            {
                case nameof(item.AutoHideCommand):
                    return item.AutoHideCommand;
                case nameof(item.HideCommand):
                    return item.HideCommand;
                case nameof(LayoutItem):
                    return item;
                default:
                    return null;
            }

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
