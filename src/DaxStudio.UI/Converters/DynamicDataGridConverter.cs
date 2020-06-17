using DaxStudio.Common;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    // Referenced by QueryResultsPaneView.xaml
    //
    public class DynamicDataGridConverter : IValueConverter
    {
        static Regex bindingPathRegex;
        static DynamicDataGridConverter()
        {
            // store the static compiled regex so we don't have to instantiate it each time we bind a column
            bindingPathRegex = new Regex(@"[\^,\]\[\.]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var columns = new ObservableCollection<DataGridColumn>();
            var dv = value as DataView;
            if (dv != null)
            {
                var dg = new DataGrid();

                //var gridView = new GridView();
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
                    txtBlock.SetValue(TextBlock.TextWrappingProperty, TextWrapping.WrapWithOverflow);
                    if (item.DataType != typeof(string)) txtBlock.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
                    
                    contentPresenter.AppendChild(txtBlock);
                    hdrTemplate.VisualTree = contentPresenter;

                    var bindingPath = FixBindingPath(item.ColumnName);

                    var cellTemplate = new DataTemplate();
                    if (item.DataType == typeof(Byte[]))
                    {
                        var style = new Style { TargetType = typeof(ToolTip) };
                        
                        //style.Setters.Add(new Setter { Property = TemplateProperty, Value = GetToolTip(dataTable) });
                        //style.Setters.Add(new Setter { Property = OverridesDefaultStyleProperty, Value = true });
                        //style.Setters.Add(new Setter { Property = System.Windows.Controls.ToolTip.HasDropShadowProperty, Value = true });
                        //Resources.Add(typeof(ToolTip), style);

                        var cellImgBlock = new FrameworkElementFactory(typeof(Image));
                        var cellTooltip = new FrameworkElementFactory(typeof(ToolTip));
                        var cellImgTooltip = new FrameworkElementFactory(typeof(Image));
                        cellImgTooltip.SetValue(Image.WidthProperty, 150d);
                        
                        cellImgBlock.SetValue(FrameworkContentElement.ToolTipProperty, cellTooltip);
                        cellTooltip.SetValue(ToolTip.ContentProperty, cellImgTooltip);

                        // Adding square brackets around the bind will escape any column names with the following "special" binding characters   . / ( ) [ ]
                        cellImgBlock.SetBinding(Image.SourceProperty, new Binding(bindingPath));
                        cellImgTooltip.SetBinding(Image.SourceProperty, new Binding(bindingPath));
                        cellImgBlock.SetValue(Image.WidthProperty, 50d);
                        
                        cellTemplate.VisualTree = cellImgBlock;
                    }
                    else
                    {
                        var cellTxtBlock = new FrameworkElementFactory(typeof(TextBlock));
                        // Adding square brackets around the bind will escape any column names with the following "special" binding characters   . / ( ) [ ]
                        var colBinding = new Binding(bindingPath);
                        cellTxtBlock.SetBinding(TextBlock.TextProperty, colBinding);

                        // TODO - this might work if I pass thru the data context as a parameter
                        // then I could call a method on the viewModel
                        //Button btn = new Button();
                        //btn.Click += (s, e) => CancelSpid(0);


                        // Bind FormatString if it exists
                        if (item.ExtendedProperties[Constants.FormatString] != null)
                            colBinding.StringFormat = item.ExtendedProperties[Constants.FormatString].ToString();
                        // set culture if it exists
                        if (item.ExtendedProperties[Constants.LocaleId] != null)
                        {
                            var cultureInfo = CultureInfo.InvariantCulture;
                            try
                            {
                                cultureInfo = new CultureInfo((int)item.ExtendedProperties[Constants.LocaleId]);
                            }
                            catch { 
                                // Do Nothing, just use the initialized value for cultureInfo 
                            }
                            colBinding.ConverterCulture = cultureInfo;
                        }
                        cellTxtBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                        if (item.DataType != typeof(string)) cellTxtBlock.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
                        cellTxtBlock.SetBinding(FrameworkElement.ToolTipProperty, colBinding );
                        cellTemplate.VisualTree = cellTxtBlock;
                        
                    }
                    
                    var dgc = new DataGridTemplateColumn
                    {
                        CellTemplate = cellTemplate,
                        //    Width = Double.NaN,    
                        HeaderTemplate = hdrTemplate,
                        Header = item.Caption,
                        SortMemberPath = item.ColumnName,
                        ClipboardContentBinding = new Binding(bindingPath)
                    };

                    columns.Add(dgc);
                    //dg.Columns.Add(gvc);
                }

                return columns;
            }
            return Binding.DoNothing;
        }

        // escapes special characters from the WPF binding path (eg. ^.][ )
        private string FixBindingPath(string columnName)
        {
            var bindingPath = bindingPathRegex.Replace(columnName, "^$0");
            return "[" + bindingPath + "]";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

