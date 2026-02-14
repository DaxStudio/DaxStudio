using ADOTabular.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DaxStudio.Interfaces
{
    public interface ITreeviewColumn
    {
        IADOTabularColumn InternalColumn { get; }
        ObservableCollection<string> SampleData { get;  }
        string MinValue { get; set; }
        string MaxValue { get; set; }
        long DistinctValues { get; set; }
        bool UpdatingBasicStats { get; set; }
        bool UpdatingSampleData { get; set; }
    }
}
