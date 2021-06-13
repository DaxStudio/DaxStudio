using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.UI.AttachedProperties
{
    public class GridSplitterController:DependencyObject
    {
        public static GridSplitter GetWatch(DependencyObject obj)
        {
            return (GridSplitter)obj.GetValue(WatchProperty);
        }

        public static void SetWatch(DependencyObject obj, GridSplitter value)
        {
            obj.SetValue(WatchProperty, value);
        }

        public static readonly DependencyProperty WatchProperty =
            DependencyProperty.RegisterAttached(
                "Watch",
                typeof(GridSplitter),
                typeof(DependencyObject),
                new UIPropertyMetadata(null, OnWatchChanged));

        private static void OnWatchChanged(DependencyObject obj,
    DependencyPropertyChangedEventArgs e)
        {
            if (obj == null) return;
            if (obj is Grid)
            {
                var grid = obj as Grid;
                var gs = e.NewValue as GridSplitter;
                if (gs != null)
                {
                    gs.IsVisibleChanged += (_sender, _e) =>
                    {
                        UpdateGrid(
                            grid,
                            (GridSplitter)_sender,
                            (bool)_e.NewValue,
                            (bool)_e.OldValue);
                    };
                }
            }
        }

        // Given: 
        static Dictionary<DependencyObject, GridLength> oldValues = new Dictionary<DependencyObject, GridLength>();
        private static void UpdateGrid(Grid grid, GridSplitter gridSplitter, bool newValue, bool oldValue)
        {
            if (newValue)
            {
                // We're visible again
                switch (gridSplitter.ResizeDirection)
                {
                    case GridResizeDirection.Columns:
                        break;
                    case GridResizeDirection.Rows:
                        int ridx = (int)gridSplitter.GetValue(Grid.RowProperty);
                        var prev = grid.RowDefinitions.ElementAt(GetPrevious(gridSplitter, ridx));
                        var curr = grid.RowDefinitions.ElementAt(GetNext(gridSplitter, ridx));
                        if (oldValues.ContainsKey(prev) && oldValues.ContainsKey(curr))
                        {
                            prev.Height = oldValues[prev];
                            curr.Height = oldValues[curr];
                        }

                        break;
                }
            }
            else
            {
                // We're being hidden
                switch (gridSplitter.ResizeDirection)
                {
                    case GridResizeDirection.Columns:
                        break;
                    case GridResizeDirection.Rows:
                        int ridx = (int)gridSplitter.GetValue(Grid.RowProperty);
                        var prev = grid.RowDefinitions.ElementAt(GetPrevious(gridSplitter, ridx));
                        var curr = grid.RowDefinitions.ElementAt(GetNext(gridSplitter, ridx));
                        switch (gridSplitter.ResizeBehavior)
                        {
                            // Naively assumes only one type of collapsing!
                            case GridResizeBehavior.PreviousAndNext:
                            case GridResizeBehavior.PreviousAndCurrent:
                                oldValues[prev] = prev.Height;
                                // add both heights to the previous grid row
                                prev.Height = new GridLength( prev.Height.Value + curr.Height.Value, prev.Height.GridUnitType);  // new GridLength(1.0, GridUnitType.Star);

                                oldValues[curr] = curr.Height;
                                curr.Height = new GridLength(0.0);
                                break;
                        }
                        break;
                }
            }
        }

        private static int GetPrevious(GridSplitter gridSplitter, int index)
        {
            switch (gridSplitter.ResizeBehavior)
            {
                case GridResizeBehavior.PreviousAndNext:
                case GridResizeBehavior.PreviousAndCurrent:
                    return index - 1;
                case GridResizeBehavior.CurrentAndNext:
                    return index;
                case GridResizeBehavior.BasedOnAlignment:
                default:
                    throw new NotSupportedException();
            }
        }

        private static int GetNext(GridSplitter gridSplitter, int index)
        {
            switch (gridSplitter.ResizeBehavior)
            {
                case GridResizeBehavior.PreviousAndCurrent:
                    return index;
                case GridResizeBehavior.PreviousAndNext:
                case GridResizeBehavior.CurrentAndNext:
                    return index + 1;
                case GridResizeBehavior.BasedOnAlignment:
                default:
                    throw new NotSupportedException();
            }
        }

    }
}
