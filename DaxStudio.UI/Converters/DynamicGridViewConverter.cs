using System;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{

    public class DynamicGridViewConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var dv = value as DataView;
            if (dv != null)
            {
                var gridView = new GridView();
                var cols = dv.ToTable().Columns;
                foreach (DataColumn item in cols)
                {
                    // This section turns off the RecogniseAccessKey setting in the column header
                    // which allows it to display underscores correctly.
                    var hdrTemplate = new DataTemplate();
                    var contentPresenter = new FrameworkElementFactory(typeof(Border));
                    contentPresenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty,false);
                    var txtBlock = new FrameworkElementFactory(typeof(TextBlock));
                    txtBlock.SetValue(TextBlock.TextProperty, item.Caption);
                    contentPresenter.AppendChild(txtBlock);
                    hdrTemplate.VisualTree = contentPresenter;

                    var cellTemplate = new DataTemplate();
                    //var cellContentPresenter = new FrameworkElementFactory(typeof(Border));
                    //cellContentPresenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, false);
                    var cellTxtBlock = new FrameworkElementFactory(typeof(TextBlock));
                    cellTxtBlock.SetBinding(TextBlock.TextProperty, new Binding(item.ColumnName) );
                    cellTxtBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                    
                    //cellContentPresenter.AppendChild(cellTxtBlock);
                    //cellTemplate.VisualTree = cellContentPresenter;
                    cellTemplate.VisualTree = cellTxtBlock;

                    var gvc = new GridViewColumn
                    {
                        CellTemplate = cellTemplate,
                        //DisplayMemberBinding = new Binding(item.ColumnName),
                        Width = Double.NaN,    
                        HeaderTemplate = hdrTemplate
                    };
                    
                    gridView.Columns.Add(gvc);
                }

                return gridView;
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

