using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Views
{
    /// <summary>
    /// Interaction logic for XmSqlErdView.xaml
    /// </summary>
    public partial class XmSqlErdView : UserControl
    {
        private bool _isDragging;
        private bool _clickedOnHeader;
        private Point _dragStartPoint;
        private Point _tableStartPosition;
        private ErdTableViewModel _draggedTable;
        private FrameworkElement _draggedElement;
        private UIElement _coordinateRoot;
        
        // Canvas panning state
        private bool _isPanningCanvas;
        private Point _panStartPoint;
        private double _panStartHorizontalOffset;
        private double _panStartVerticalOffset;

        public XmSqlErdView()
        {
            InitializeComponent();
            
            // Subscribe to export requests and scale changes from the ViewModel
            DataContextChanged += (s, e) =>
            {
                if (e.OldValue is XmSqlErdViewModel oldVm)
                {
                    oldVm.ExportRequested -= OnExportRequested;
                    oldVm.CopyImageRequested -= OnCopyImageRequested;
                    oldVm.OnScaleChanged -= OnScaleChanged;
                }
                if (e.NewValue is XmSqlErdViewModel newVm)
                {
                    newVm.ExportRequested += OnExportRequested;
                    newVm.CopyImageRequested += OnCopyImageRequested;
                    newVm.OnScaleChanged += OnScaleChanged;
                    // Set initial view dimensions
                    UpdateViewDimensions(newVm);
                    // Apply initial scale
                    UpdateCanvasScale(newVm.Scale);
                }
            };
            
            // Update view dimensions when size changes
            SizeChanged += (s, e) =>
            {
                if (DataContext is XmSqlErdViewModel vm)
                {
                    UpdateViewDimensions(vm);
                }
            };
        }

        /// <summary>
        /// Updates the ViewModel with current view dimensions for ZoomToFit calculation.
        /// </summary>
        private void UpdateViewDimensions(XmSqlErdViewModel vm)
        {
            if (MainScrollViewer != null)
            {
                vm.ViewWidth = MainScrollViewer.ActualWidth > 0 ? MainScrollViewer.ActualWidth : 800;
                vm.ViewHeight = MainScrollViewer.ActualHeight > 0 ? MainScrollViewer.ActualHeight : 600;
            }
        }

        /// <summary>
        /// Handles scale changes from the ViewModel to update the canvas transform.
        /// </summary>
        private void OnScaleChanged(object sender, System.EventArgs e)
        {
            if (DataContext is XmSqlErdViewModel vm)
            {
                UpdateCanvasScale(vm.Scale);
            }
        }

        /// <summary>
        /// Updates the canvas scale transform.
        /// </summary>
        private void UpdateCanvasScale(double scale)
        {
            if (CanvasScaleTransform != null)
            {
                CanvasScaleTransform.ScaleX = scale;
                CanvasScaleTransform.ScaleY = scale;
            }
        }

        /// <summary>
        /// Handles mouse wheel for zooming when Ctrl is held (only affects canvas, not toolbar).
        /// </summary>
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (DataContext is XmSqlErdViewModel vm)
                {
                    var factor = e.Delta / 1200.0;
                    vm.Scale += factor;
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Handles export request from ViewModel by rendering the canvas to a PNG file.
        /// </summary>
        private void OnExportRequested(object sender, string filePath)
        {
            try
            {
                // Find the canvas to render - use the DiagramCanvas named in XAML
                var canvas = DiagramCanvas;
                if (canvas == null) return;

                // Get the actual size of the content
                var bounds = VisualTreeHelper.GetDescendantBounds(canvas);
                if (bounds.IsEmpty) 
                {
                    bounds = new Rect(0, 0, canvas.ActualWidth, canvas.ActualHeight);
                }

                // Create a render target with proper DPI
                var dpi = 96d;
                var renderWidth = (int)System.Math.Max(bounds.Width + 40, canvas.ActualWidth);
                var renderHeight = (int)System.Math.Max(bounds.Height + 40, canvas.ActualHeight);
                
                var renderTarget = new RenderTargetBitmap(
                    renderWidth, renderHeight,
                    dpi, dpi,
                    PixelFormats.Pbgra32);

                // Create a visual brush to render from the canvas with background
                var drawingVisual = new DrawingVisual();
                using (var dc = drawingVisual.RenderOpen())
                {
                    // Draw white background
                    dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, renderWidth, renderHeight));
                    
                    // Draw the canvas content
                    var visualBrush = new VisualBrush(canvas);
                    dc.DrawRectangle(visualBrush, null, new Rect(0, 0, canvas.ActualWidth, canvas.ActualHeight));
                }
                
                renderTarget.Render(drawingVisual);

                // Encode to PNG
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                // Save to file
                using (var stream = File.Create(filePath))
                {
                    encoder.Save(stream);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to export image: {ex.Message}", "Export Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles copy image to clipboard request from ViewModel.
        /// </summary>
        private void OnCopyImageRequested(object sender, EventArgs e)
        {
            try
            {
                var canvas = DiagramCanvas;
                if (canvas == null) return;

                // Get the actual size of the content
                var bounds = VisualTreeHelper.GetDescendantBounds(canvas);
                if (bounds.IsEmpty) 
                {
                    bounds = new Rect(0, 0, canvas.ActualWidth, canvas.ActualHeight);
                }

                // Create a render target with proper DPI
                var dpi = 96d;
                var renderWidth = (int)System.Math.Max(bounds.Width + 40, canvas.ActualWidth);
                var renderHeight = (int)System.Math.Max(bounds.Height + 40, canvas.ActualHeight);
                
                var renderTarget = new RenderTargetBitmap(
                    renderWidth, renderHeight,
                    dpi, dpi,
                    PixelFormats.Pbgra32);

                // Create a visual brush to render from the canvas with background
                var drawingVisual = new DrawingVisual();
                using (var dc = drawingVisual.RenderOpen())
                {
                    // Draw white background
                    dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, renderWidth, renderHeight));
                    
                    // Draw the canvas content
                    var visualBrush = new VisualBrush(canvas);
                    dc.DrawRectangle(visualBrush, null, new Rect(0, 0, canvas.ActualWidth, canvas.ActualHeight));
                }
                
                renderTarget.Render(drawingVisual);

                // Copy to clipboard
                Clipboard.SetImage(renderTarget);
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to copy image to clipboard: {ex.Message}", "Copy Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Find the coordinate root - the Grid inside ScrollViewer that has the canvas dimensions.
        /// </summary>
        private UIElement FindCoordinateRoot(DependencyObject element)
        {
            // Walk up the tree to find the ScrollViewer, then get its content (the Grid)
            while (element != null)
            {
                if (element is ScrollViewer scrollViewer)
                {
                    return scrollViewer.Content as UIElement;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private void Table_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ErdTableViewModel tableVm)
            {
                _coordinateRoot = FindCoordinateRoot(element);
                if (_coordinateRoot == null) return;

                // Start drag operation - use position relative to the coordinate root
                _isDragging = true;
                _dragStartPoint = e.GetPosition(_coordinateRoot);
                _tableStartPosition = new Point(tableVm.X, tableVm.Y);
                _draggedTable = tableVm;
                _draggedElement = element;
                element.CaptureMouse();

                // Also select the table when clicked
                if (DataContext is XmSqlErdViewModel erdVm)
                {
                    erdVm.SelectTable(tableVm.TableName);
                }

                e.Handled = true;
            }
        }

        private void Table_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _draggedTable != null && _coordinateRoot != null)
            {
                var currentPosition = e.GetPosition(_coordinateRoot);
                
                // Calculate new position based on how far we've moved from the start
                var newX = _tableStartPosition.X + (currentPosition.X - _dragStartPoint.X);
                var newY = _tableStartPosition.Y + (currentPosition.Y - _dragStartPoint.Y);

                // Keep within canvas bounds (don't go negative)
                if (newX < 0) newX = 0;
                if (newY < 0) newY = 0;

                // Update table position
                _draggedTable.X = newX;
                _draggedTable.Y = newY;

                // Notify ViewModel to update relationship lines
                if (DataContext is XmSqlErdViewModel erdVm)
                {
                    erdVm.OnTablePositionChanged(_draggedTable);
                }

                e.Handled = true;
            }
        }

        private void Table_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var wasDragging = _isDragging;
                var dragDistance = 0.0;
                
                if (_isDragging && _coordinateRoot != null)
                {
                    var currentPos = e.GetPosition(_coordinateRoot);
                    dragDistance = (currentPos - _dragStartPoint).Length;
                }
                
                // Check if this was a header click (show table details if didn't drag)
                if (_clickedOnHeader && element.DataContext is ErdTableViewModel tableVm)
                {
                    // If we didn't actually move much, treat as a click and show details
                    if (dragDistance < 5)
                    {
                        if (DataContext is XmSqlErdViewModel erdVm)
                        {
                            erdVm.SelectTableDetails(tableVm);
                        }
                    }
                }
                
                _isDragging = false;
                _clickedOnHeader = false;
                _draggedTable = null;
                _draggedElement = null;
                _coordinateRoot = null;
                element.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Click on empty canvas area - start panning or clear selection
            if (e.OriginalSource is FrameworkElement fe && 
                !(fe.DataContext is ErdTableViewModel))
            {
                // Start canvas panning
                _isPanningCanvas = true;
                _panStartPoint = e.GetPosition(MainScrollViewer);
                _panStartHorizontalOffset = MainScrollViewer.HorizontalOffset;
                _panStartVerticalOffset = MainScrollViewer.VerticalOffset;
                
                // Capture mouse on the canvas grid
                if (sender is FrameworkElement element)
                {
                    element.CaptureMouse();
                    element.Cursor = Cursors.Hand;
                }
                
                // Also clear selection when clicking empty area
                if (DataContext is XmSqlErdViewModel erdVm)
                {
                    erdVm.ClearSelection();
                }
                
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanningCanvas && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(MainScrollViewer);
                var deltaX = currentPoint.X - _panStartPoint.X;
                var deltaY = currentPoint.Y - _panStartPoint.Y;
                
                // Scroll in the opposite direction of mouse movement (natural panning)
                MainScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - deltaX);
                MainScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - deltaY);
                
                e.Handled = true;
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanningCanvas)
            {
                _isPanningCanvas = false;
                
                if (sender is FrameworkElement element)
                {
                    element.ReleaseMouseCapture();
                    element.Cursor = Cursors.Arrow;
                }
                
                e.Handled = true;
            }
        }

        private void Column_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ErdColumnViewModel columnVm)
            {
                // Find the parent table view model
                var parent = element;
                while (parent != null)
                {
                    if (parent.DataContext is ErdTableViewModel tableVm)
                    {
                        if (DataContext is XmSqlErdViewModel erdVm)
                        {
                            erdVm.SelectColumn(tableVm, columnVm);
                        }
                        break;
                    }
                    parent = VisualTreeHelper.GetParent(parent) as FrameworkElement;
                }
                
                e.Handled = true;
            }
        }

        private void Relationship_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ErdRelationshipViewModel relVm)
            {
                if (DataContext is XmSqlErdViewModel erdVm)
                {
                    erdVm.SelectRelationship(relVm);
                }
                e.Handled = true;
            }
        }

        private void Relationship_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ErdRelationshipViewModel relVm)
            {
                if (DataContext is XmSqlErdViewModel erdVm)
                {
                    erdVm.OnRelationshipMouseEnter(relVm);
                }
            }
        }

        private void Relationship_MouseLeave(object sender, MouseEventArgs e)
        {
            if (DataContext is XmSqlErdViewModel erdVm)
            {
                erdVm.OnRelationshipMouseLeave();
            }
        }

        private void TableHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ErdTableViewModel tableVm)
            {
                // Double-click toggles collapse
                if (e.ClickCount == 2)
                {
                    tableVm.ToggleCollapse();
                    // Update relationships connected to this table
                    if (DataContext is XmSqlErdViewModel erdVm)
                    {
                        erdVm.OnTablePositionChanged(tableVm);
                    }
                    e.Handled = true;
                    return;
                }

                // Mark that we clicked on the header (for showing details on mouse up)
                _clickedOnHeader = true;
                
                // Let the event bubble up to start dragging
                // Don't set e.Handled = true here so drag can work
            }
        }

        private void TableHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // This may not fire if mouse was captured by parent - logic moved to Table_MouseLeftButtonUp
        }

        private void CollapseToggle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ErdTableViewModel tableVm)
            {
                tableVm.ToggleCollapse();
                // Update relationships connected to this table
                if (DataContext is XmSqlErdViewModel erdVm)
                {
                    erdVm.OnTablePositionChanged(tableVm);
                }
                e.Handled = true;
            }
        }

        #region Table Resize

        private bool _isResizing;
        private Point _resizeStartPoint;
        private double _resizeStartHeight;
        private ErdTableViewModel _resizingTable;
        private FrameworkElement _resizingElement;

        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ErdTableViewModel tableVm)
            {
                _coordinateRoot = FindCoordinateRoot(element);
                if (_coordinateRoot == null) return;

                _isResizing = true;
                _resizeStartPoint = e.GetPosition(_coordinateRoot);
                _resizeStartHeight = tableVm.ColumnsHeight;
                _resizingTable = tableVm;
                _resizingElement = element;
                element.CaptureMouse();
                e.Handled = true;
            }
        }

        private void ResizeGrip_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isResizing && _resizingTable != null && _coordinateRoot != null)
            {
                var currentPosition = e.GetPosition(_coordinateRoot);
                var deltaY = currentPosition.Y - _resizeStartPoint.Y;
                
                // Update the columns height (this also updates ExpandedHeight)
                _resizingTable.ColumnsHeight = _resizeStartHeight + deltaY;
                
                // Update relationship lines connected to this table
                if (DataContext is XmSqlErdViewModel erdVm)
                {
                    erdVm.OnTablePositionChanged(_resizingTable);
                }
                e.Handled = true;
            }
        }

        private void ResizeGrip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isResizing && sender is FrameworkElement element)
            {
                _isResizing = false;
                _resizingTable = null;
                _resizingElement = null;
                _coordinateRoot = null;
                element.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        #endregion

        #region Horizontal Table Resize (Width)

        private bool _isHorizontalResizing;
        private Point _horizontalResizeStartPoint;
        private double _resizeStartWidth;
        private ErdTableViewModel _horizontalResizingTable;

        private void HorizontalResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ErdTableViewModel tableVm)
            {
                _coordinateRoot = FindCoordinateRoot(element);
                if (_coordinateRoot == null) return;

                _isHorizontalResizing = true;
                _horizontalResizeStartPoint = e.GetPosition(_coordinateRoot);
                _resizeStartWidth = tableVm.Width;
                _horizontalResizingTable = tableVm;
                element.CaptureMouse();
                e.Handled = true;
            }
        }

        private void HorizontalResizeGrip_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isHorizontalResizing && _horizontalResizingTable != null && _coordinateRoot != null)
            {
                var currentPosition = e.GetPosition(_coordinateRoot);
                var deltaX = currentPosition.X - _horizontalResizeStartPoint.X;
                
                // Update the table width (with minimum constraint)
                var newWidth = Math.Max(150, _resizeStartWidth + deltaX);
                _horizontalResizingTable.Width = newWidth;
                
                // Update relationship lines connected to this table
                if (DataContext is XmSqlErdViewModel erdVm)
                {
                    erdVm.OnTablePositionChanged(_horizontalResizingTable);
                }
                e.Handled = true;
            }
        }

        private void HorizontalResizeGrip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isHorizontalResizing && sender is FrameworkElement element)
            {
                _isHorizontalResizing = false;
                _horizontalResizingTable = null;
                _coordinateRoot = null;
                element.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        #endregion

        #region Mini-map Navigation

        private bool _isMiniMapDragging;

        /// <summary>
        /// Handles scroll changes to update the mini-map viewport rectangle.
        /// </summary>
        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateMiniMapViewport();
            
            // Update content bounds for mini-map
            if (DataContext is XmSqlErdViewModel vm)
            {
                vm.UpdateContentBounds();
                UpdateTableMiniMapScales(vm);
            }
        }

        /// <summary>
        /// Updates the mini-map viewport rectangle based on current scroll position.
        /// </summary>
        private void UpdateMiniMapViewport()
        {
            if (MiniMapViewport == null || MiniMapCanvas == null || !(DataContext is XmSqlErdViewModel vm))
                return;

            // Calculate viewport position and size in mini-map coordinates
            var contentWidth = vm.ContentWidth > 0 ? vm.ContentWidth : 1000;
            var contentHeight = vm.ContentHeight > 0 ? vm.ContentHeight : 800;

            var scaleX = MiniMapCanvas.Width / contentWidth;
            var scaleY = MiniMapCanvas.Height / contentHeight;

            // Get actual viewport dimensions (accounting for zoom)
            var viewportWidth = MainScrollViewer.ViewportWidth / vm.Scale;
            var viewportHeight = MainScrollViewer.ViewportHeight / vm.Scale;

            // Get scroll position (accounting for zoom)
            var scrollX = MainScrollViewer.HorizontalOffset / vm.Scale;
            var scrollY = MainScrollViewer.VerticalOffset / vm.Scale;

            // Set viewport rectangle position and size
            Canvas.SetLeft(MiniMapViewport, scrollX * scaleX);
            Canvas.SetTop(MiniMapViewport, scrollY * scaleY);
            MiniMapViewport.Width = System.Math.Max(4, viewportWidth * scaleX);
            MiniMapViewport.Height = System.Math.Max(3, viewportHeight * scaleY);

            // Update ViewModel viewport position
            vm.ViewportX = scrollX;
            vm.ViewportY = scrollY;
        }

        /// <summary>
        /// Updates the mini-map scale for all tables.
        /// </summary>
        private void UpdateTableMiniMapScales(XmSqlErdViewModel vm)
        {
            if (MiniMapCanvas == null) return;

            var scaleX = MiniMapCanvas.Width / (vm.ContentWidth > 0 ? vm.ContentWidth : 1000);
            var scaleY = MiniMapCanvas.Height / (vm.ContentHeight > 0 ? vm.ContentHeight : 800);

            foreach (var table in vm.Tables)
            {
                table.SetMiniMapScale(scaleX, scaleY);
            }
        }

        private void MiniMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isMiniMapDragging = true;
            MiniMapCanvas?.CaptureMouse();
            NavigateToMiniMapPoint(e.GetPosition(MiniMapCanvas));
            e.Handled = true;
        }

        private void MiniMap_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isMiniMapDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                NavigateToMiniMapPoint(e.GetPosition(MiniMapCanvas));
                e.Handled = true;
            }
        }

        private void MiniMap_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isMiniMapDragging)
            {
                _isMiniMapDragging = false;
                MiniMapCanvas?.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Navigates the main scroll viewer to the position clicked on the mini-map.
        /// </summary>
        private void NavigateToMiniMapPoint(Point miniMapPoint)
        {
            if (!(DataContext is XmSqlErdViewModel vm) || MiniMapCanvas == null)
                return;

            // Convert mini-map coordinates to content coordinates
            var contentWidth = vm.ContentWidth > 0 ? vm.ContentWidth : 1000;
            var contentHeight = vm.ContentHeight > 0 ? vm.ContentHeight : 800;

            var scaleX = contentWidth / MiniMapCanvas.Width;
            var scaleY = contentHeight / MiniMapCanvas.Height;

            var contentX = miniMapPoint.X * scaleX;
            var contentY = miniMapPoint.Y * scaleY;

            // Center the viewport on this point
            var viewportWidth = MainScrollViewer.ViewportWidth / vm.Scale;
            var viewportHeight = MainScrollViewer.ViewportHeight / vm.Scale;

            var scrollX = (contentX - viewportWidth / 2) * vm.Scale;
            var scrollY = (contentY - viewportHeight / 2) * vm.Scale;

            MainScrollViewer.ScrollToHorizontalOffset(System.Math.Max(0, scrollX));
            MainScrollViewer.ScrollToVerticalOffset(System.Math.Max(0, scrollY));
        }

        #endregion

        #region Detail Panel Resize

        private bool _isDetailPanelResizing;
        private double _detailPanelStartWidth;
        private Point _detailPanelResizeStart;

        private void DetailPanel_ResizeStart(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is XmSqlErdViewModel vm)
            {
                _isDetailPanelResizing = true;
                _detailPanelStartWidth = vm.DetailPanelWidth;
                _detailPanelResizeStart = e.GetPosition(this);
                ((UIElement)sender).CaptureMouse();
                e.Handled = true;
            }
        }

        private void DetailPanel_ResizeMove(object sender, MouseEventArgs e)
        {
            if (_isDetailPanelResizing && DataContext is XmSqlErdViewModel vm)
            {
                var currentPos = e.GetPosition(this);
                // Dragging left increases width, dragging right decreases
                var delta = _detailPanelResizeStart.X - currentPos.X;
                vm.DetailPanelWidth = _detailPanelStartWidth + delta;
                e.Handled = true;
            }
        }

        private void DetailPanel_ResizeEnd(object sender, MouseButtonEventArgs e)
        {
            if (_isDetailPanelResizing)
            {
                _isDetailPanelResizing = false;
                ((UIElement)sender).ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        #endregion
    }
}
