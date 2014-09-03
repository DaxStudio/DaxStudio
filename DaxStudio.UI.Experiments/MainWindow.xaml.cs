using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DaxStudio.UI.Experiments
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // mock up a model 
            var c = new ADOTabular.ADOTabularConnection("Data Source=localhost",ADOTabular.AdomdClientWrappers.AdomdType.AnalysisServices);
            var m = new ADOTabular.ADOTabularModel(c, "Model", "Model Desc", "Model");
            var t1 = new ADOTabular.ADOTabularTable(c,"Table1","Table1 Desc",true);
            var c1 = new ADOTabular.ADOTabularStandardColumn(t1,"c1","Column1","Column1 Desc",true, ADOTabular.ADOTabularColumnType.Column,"");
            var c2 = new ADOTabular.ADOTabularStandardColumn(t1, "c2", "Column2", "Column2 Desc", true, ADOTabular.ADOTabularColumnType.Column, "");
            var c3 = new ADOTabular.ADOTabularColumn(t1, "c3", "Column3", "Column3 Desc", true, ADOTabular.ADOTabularColumnType.Column, "");
            t1.Columns.Add(c1);
            t1.Columns.Add(c2);
            t1.Columns.Add(c3);
            var h1 = new ADOTabular.ADOTabularHierarchy(t1, "h1", "Hier1", "Hier1 Desc", true, ADOTabular.ADOTabularColumnType.Hierarchy, "");
            h1.Levels.Add( new ADOTabular.ADOTabularLevel( c3));
            t1.Columns.Add(h1);
            m.Tables.Add(t1);
            var t2 = new ADOTabular.ADOTabularTable(c, "Table2", "Table2 Desc", true);
            m.Tables.Add(t2);
            PresentationTraceSources.DataBindingSource.Listeners.Add(
                    new ConsoleTraceListener());

            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.All;

            Model = m;
            Tables = m.Tables;
            base.DataContext = Model;
        }

        public ADOTabular.ADOTabularModel Model
        {
            get;
            set;
        }

        public ADOTabular.ADOTabularTableCollection Tables { get; set; }

    }
}
