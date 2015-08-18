using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace DaxStudio.UI.Behaviours
{
  public class ListViewSortBehaviour
  {
      #region GridViewSort
      public static DependencyProperty GridViewSortPropertyNameProperty =
          DependencyProperty.RegisterAttached(
              "GridViewSortPropertyName", 
              typeof(string), 
              typeof(ListViewSortBehaviour), 
              new UIPropertyMetadata(null)
          );

      public static string GetGridViewSortPropertyName(GridViewColumn gvc)
      {
          return (string)gvc.GetValue(GridViewSortPropertyNameProperty);
      }

      public static void SetGridViewSortPropertyName(GridViewColumn gvc, string n)
      {
          gvc.SetValue(GridViewSortPropertyNameProperty, n);
      }

      public static DependencyProperty CurrentSortColumnProperty =
          DependencyProperty.RegisterAttached(
              "CurrentSortColumn", 
              typeof(GridViewColumn), 
              typeof(ListViewSortBehaviour), 
              new UIPropertyMetadata(
                  null, 
                  new PropertyChangedCallback(CurrentSortColumnChanged)
              )
          );

      public static GridViewColumn GetCurrentSortColumn(GridView gv)
      {
          return (GridViewColumn)gv.GetValue(CurrentSortColumnProperty);
      }

      public static void SetCurrentSortColumn(GridView gv, GridViewColumn value)
      {
          gv.SetValue(CurrentSortColumnProperty, value);
      }

      public static void CurrentSortColumnChanged(
          object sender, DependencyPropertyChangedEventArgs e)
      {
          GridViewColumn gvcOld = e.OldValue as GridViewColumn;
          if (gvcOld != null)
          {
              CurrentSortColumnSetGlyph(gvcOld, null);
          }
      }

      public static void CurrentSortColumnSetGlyph(GridViewColumn gvc, ListView lv)
      {
          ListSortDirection lsd;
          Brush brush;
          if (lv == null)
          {
              lsd = ListSortDirection.Ascending;
              brush = Brushes.Transparent;
          }
          else
          {
              SortDescriptionCollection sdc = lv.Items.SortDescriptions;
              if (sdc == null || sdc.Count < 1) return;
              lsd = sdc[0].Direction;
              brush = Brushes.Gray;
          }

          FrameworkElementFactory fefGlyph = 
              new FrameworkElementFactory(typeof(Path));
          fefGlyph.Name = "arrow";
          fefGlyph.SetValue(Path.StrokeThicknessProperty, 1.0);
          fefGlyph.SetValue(Path.FillProperty, brush);
          fefGlyph.SetValue(StackPanel.HorizontalAlignmentProperty, 
              HorizontalAlignment.Center);

          int s = 4;
          if (lsd == ListSortDirection.Ascending)
          {
              PathFigure pf = new PathFigure();
              pf.IsClosed = true;
              pf.StartPoint = new Point(0, s);
              pf.Segments.Add(new LineSegment(new Point(s * 2, s), false));
              pf.Segments.Add(new LineSegment(new Point(s, 0), false));

              PathGeometry pg = new PathGeometry();
              pg.Figures.Add(pf);

              fefGlyph.SetValue(Path.DataProperty, pg);
          }
          else
          {
              PathFigure pf = new PathFigure();
              pf.IsClosed = true;
              pf.StartPoint = new Point(0, 0);
              pf.Segments.Add(new LineSegment(new Point(s, s), false));
              pf.Segments.Add(new LineSegment(new Point(s * 2, 0), false));

              PathGeometry pg = new PathGeometry();
              pg.Figures.Add(pf);

              fefGlyph.SetValue(Path.DataProperty, pg);
          }

          FrameworkElementFactory fefTextBlock = 
              new FrameworkElementFactory(typeof(TextBlock));
          fefTextBlock.SetValue(TextBlock.HorizontalAlignmentProperty,
              HorizontalAlignment.Center);
          fefTextBlock.SetValue(TextBlock.TextProperty, new Binding());

          FrameworkElementFactory fefDockPanel = 
              new FrameworkElementFactory(typeof(StackPanel));
          fefDockPanel.SetValue(StackPanel.OrientationProperty,
              Orientation.Vertical);
          fefDockPanel.AppendChild(fefGlyph);
          fefDockPanel.AppendChild(fefTextBlock);

          DataTemplate dt = new DataTemplate(typeof(GridViewColumn));
          dt.VisualTree = fefDockPanel;

          gvc.HeaderTemplate = dt;
      }

      public static DependencyProperty EnableGridViewSortProperty =
          DependencyProperty.RegisterAttached(
              "EnableGridViewSort", 
              typeof(bool), 
              typeof(ListViewSortBehaviour), 
              new UIPropertyMetadata(
                  false, 
                  new PropertyChangedCallback(EnableGridViewSortChanged)
              )
          );

      public static bool GetEnableGridViewSort(ListView lv)
      {
          return (bool)lv.GetValue(EnableGridViewSortProperty);
      }

      public static void SetEnableGridViewSort(ListView lv, bool value)
      {
          lv.SetValue(EnableGridViewSortProperty, value);
      }

      public static void EnableGridViewSortChanged(
          object sender, DependencyPropertyChangedEventArgs e)
      {
          ListView lv = sender as ListView;
          if (lv == null) return;

          if (!(e.NewValue is bool)) return;
          bool enableGridViewSort = (bool)e.NewValue;

          if (enableGridViewSort)
          {
              lv.AddHandler(
                  GridViewColumnHeader.ClickEvent,
                  new RoutedEventHandler(EnableGridViewSortGVHClicked)
              );
              if (lv.View == null)
              {
                  lv.Loaded += new RoutedEventHandler(EnableGridViewSortLVLoaded);
              }
              else
              {
                  EnableGridViewSortLVInitialize(lv);
              }
          }
          else
          {
              lv.RemoveHandler(
                  GridViewColumnHeader.ClickEvent,
                  new RoutedEventHandler(EnableGridViewSortGVHClicked)
              );
          }
      }

      public static void EnableGridViewSortLVLoaded(object sender, RoutedEventArgs e)
      {
          ListView lv = e.Source as ListView;
          EnableGridViewSortLVInitialize(lv);
          lv.Loaded -= new RoutedEventHandler(EnableGridViewSortLVLoaded);
      }

      public static void EnableGridViewSortLVInitialize(ListView lv)
      {
          GridView gv = lv.View as GridView;
          if (gv == null) return;

          bool first = true;
          foreach (GridViewColumn gvc in gv.Columns)
          {
              if (first)
              {
                  EnableGridViewSortApplySort(lv, gv, gvc);
                  first = false;
              }
              else
              {
                  CurrentSortColumnSetGlyph(gvc, null);
              }
          }
      }

      public static void EnableGridViewSortGVHClicked(
          object sender, RoutedEventArgs e)
      {
          GridViewColumnHeader gvch = e.OriginalSource as GridViewColumnHeader;
          if (gvch == null) return;
          GridViewColumn gvc = gvch.Column;
          if(gvc == null) return;            
          ListView lv = VisualUpwardSearch<ListView>(gvch);
          if (lv == null) return;
          GridView gv = lv.View as GridView;
          if (gv == null) return;

          EnableGridViewSortApplySort(lv, gv, gvc);
      }

      public static void EnableGridViewSortApplySort(
          ListView lv, GridView gv, GridViewColumn gvc)
      {
          bool isEnabled = GetEnableGridViewSort(lv);
          if (!isEnabled) return;

          string propertyName = GetGridViewSortPropertyName(gvc);
          if (string.IsNullOrEmpty(propertyName))
          {
              Binding b = gvc.DisplayMemberBinding as Binding;
              if (b != null && b.Path != null)
              {
                  propertyName = b.Path.Path;
              }

              if (string.IsNullOrEmpty(propertyName)) return;
          }

          ApplySort(lv.Items, propertyName);
          SetCurrentSortColumn(gv, gvc);
          CurrentSortColumnSetGlyph(gvc, lv);
      }

      public static void ApplySort(ICollectionView view, string propertyName)
      {
          if (string.IsNullOrEmpty(propertyName)) return;

          ListSortDirection lsd = ListSortDirection.Ascending;
          if (view.SortDescriptions.Count > 0)
          {
              SortDescription sd = view.SortDescriptions[0];
              if (sd.PropertyName.Equals(propertyName))
              {
                  if (sd.Direction == ListSortDirection.Ascending)
                  {
                      lsd = ListSortDirection.Descending;
                  }
                  else
                  {
                      lsd = ListSortDirection.Ascending;
                  }
              }
              view.SortDescriptions.Clear();
          }

          view.SortDescriptions.Add(new SortDescription(propertyName, lsd));
      }
      #endregion

      public static T VisualUpwardSearch<T>(DependencyObject source) 
          where T : DependencyObject
      {
          return VisualUpwardSearch(source, x => x is T) as T;
      }

      public static DependencyObject VisualUpwardSearch(
                          DependencyObject source, Predicate<DependencyObject> match)
      {
          DependencyObject returnVal = source;

          while (returnVal != null && !match(returnVal))
          {
              DependencyObject tempReturnVal = null;
              if (returnVal is Visual || returnVal is Visual3D)
              {
                  tempReturnVal = VisualTreeHelper.GetParent(returnVal);
              }
              if (tempReturnVal == null)
              {
                  returnVal = LogicalTreeHelper.GetParent(returnVal);
              }
              else
              {
                  returnVal = tempReturnVal;
              }
          }

          return returnVal;
      }
  }
}