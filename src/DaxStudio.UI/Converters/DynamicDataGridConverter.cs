using DaxStudio.Common;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Text;
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

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var columns = new ObservableCollection<DataGridColumn>();
            
            if (value is DataView dv)
            {
                

                //var gridView = new GridView();
                var cols = dv.ToTable().Columns;
                foreach (DataColumn item in cols)
                {
                    // This section turns off the RecogniseAccessKey setting in the column header
                    // which allows it to display underscores correctly.
                    var hdrTemplate = new DataTemplate();
                    var contentPresenter = new FrameworkElementFactory(typeof(Border));
                    contentPresenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty,false);
                    var txtBlock = new FrameworkElementFactory(typeof(TextBlock), "HeaderText") ;
                    
                    txtBlock.SetValue(TextBlock.TextProperty, item.Caption);
                    txtBlock.SetValue(TextBlock.TextWrappingProperty, TextWrapping.WrapWithOverflow);
                    
                    // if there is 1 extended property add it to the tag for the text box
                    if (item.ExtendedProperties.Count == 1)
                    {
                        foreach (DictionaryEntry prop in item.ExtendedProperties)
                        {
                            txtBlock.SetValue(TextBlock.TagProperty, prop.Value);
                        }
                    }
                    
                    if (item.DataType != typeof(string)) txtBlock.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
                    
                    contentPresenter.AppendChild(txtBlock);
                    hdrTemplate.VisualTree = contentPresenter;

                    var bindingPath = FixBindingPath(item.ColumnName);

                    var cellTemplate = new DataTemplate();
                    Binding columnBinding = null;
                    Binding clipboardBinding = null;
                    if (item.DataType == typeof(Byte[]))
                    {

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

                        cellTxtBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                        cellTxtBlock.SetValue(TextBlock.PaddingProperty, new Thickness(6, 3, 6, 0));
                        if (item.DataType != typeof(string)) cellTxtBlock.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);

                        // Adding square brackets around the bind will escape any column names with the following "special" binding characters   . / ( ) [ ]
                        try
                        {
                            columnBinding = new Binding(bindingPath);
                            cellTxtBlock.SetBinding(TextBlock.TextProperty, columnBinding);

                            // TODO - this might work if I pass thru the data context as a parameter
                            // then I could call a method on the viewModel
                            //Button btn = new Button();
                            //btn.Click += (s, e) => CancelSpid(0);


                            // Bind FormatString if it exists
                            if (item.ExtendedProperties[Constants.FormatString] != null)
                                columnBinding.StringFormat = item.ExtendedProperties[Constants.FormatString].ToString();
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
                                columnBinding.ConverterCulture = cultureInfo;
                            }

                            cellTxtBlock.SetBinding(FrameworkElement.ToolTipProperty, columnBinding);
                        }
                        catch (Exception ex)
                        {
                            cellTxtBlock.SetValue(TextBlock.TextProperty, "Error: " + ex.Message);

                            var errorBrushDynamicResource = new DynamicResourceExtension("Theme.Brush.Log.Error");
                            cellTxtBlock.SetResourceReference(TextBlock.ForegroundProperty, errorBrushDynamicResource.ResourceKey);

                            var fixedStringConverter = new FixedStringConverter();
                            clipboardBinding =  new Binding {Converter = fixedStringConverter, ConverterParameter = "Error: " + ex.Message };

                        }
                        cellTemplate.VisualTree = cellTxtBlock;
                        
                    }
                    
                    var dgc = new DataGridTemplateColumn
                    {
                        CellTemplate = cellTemplate,
                        HeaderTemplate = hdrTemplate,
                        Header = item.Caption,
                        SortMemberPath = item.ColumnName,
                        ClipboardContentBinding = (BindingBase)(columnBinding ?? clipboardBinding)
                    };

                    if (columnBinding == null)

                    columns.Add(dgc);
                }

                return columns;
            }
            return Binding.DoNothing;
        }

        // escapes special characters from the WPF binding path (eg. ^.][ )
        private string FixBindingPath(string columnName)
        {
            var sb = new StringBuilder();
            char escape = '^';
            sb.Append('[');
            foreach(char c in columnName.ToCharArray())
            {
                switch (c)
                {
                    case '&':
                    case '>':
                    case '=':
                    case ',':
                    case '}':
                    case ']':
                    case '[':
                    case '.':
                    case '^':
                    case '\\': 
                        sb.Append(escape);
                        sb.Append(c);
                        break;
                    
                    default: sb.Append(c); break;
                }
            }
            sb.Append(']');
            return sb.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

