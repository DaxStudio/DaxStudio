using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;
using System.Windows.Data;
using System;
using System.ComponentModel;
using Serilog;
using System.Windows.Input;
using DaxStudio.Interfaces;
using Dax.ViewModel;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Windows.Navigation;

namespace DaxStudio.UI.ViewModels
{
    public class VpaRelationshipViewModel
    {
        VpaRelationship _rel;
        public VpaRelationshipViewModel(VpaRelationship rel, VpaTableViewModel table)
        {
            Table = table;
            _rel = rel;
        }
        public VpaTableViewModel Table { get; }
        public string RelationshipFromToName => _rel.RelationshipFromToName;
        public long UsedSize => _rel.UsedSize;
        public long FromColumnCardinality => _rel.FromColumnCardinality;
        public long ToColumnCardinality => _rel.ToColumnCardinality;
        public long MissingKeys => _rel.MissingKeys;
        public long InvalidRows => _rel.InvalidRows;
        public string SampleReferentialIntegrityViolations => _rel.SampleReferentialIntegrityViolations;
        public double OneToManyRatio => _rel.OneToManyRatio;
    }
}
