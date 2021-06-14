using ADOTabular;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ADOTabular.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DaxStudio.UI.Model
{
    [DataContract]
    public class QueryBuilderFilter : PropertyChangedBase
    {

        public QueryBuilderFilter(IADOTabularColumn obj, IModelCapabilities modelCapabilities, IEventAggregator eventAggregator)
        {
            TabularObject = obj;
            ModelCapabilities = modelCapabilities;
            EventAggregator = eventAggregator;
        }
        [DataMember]
        public IADOTabularColumn TabularObject { get; }
        [DataMember]
        public IModelCapabilities ModelCapabilities { get; }
        public IEventAggregator EventAggregator { get; }

        public string Caption => TabularObject.Caption;

        private FilterType _fitlerType;
        [DataMember,JsonConverter(typeof(StringEnumConverter))]
        public FilterType FilterType 
        {  
            get => _fitlerType;
            set {
                _fitlerType = value;
                NotifyOfPropertyChange(nameof(FilterType));
                NotifyOfPropertyChange(nameof(ShowFilterValue));
                NotifyOfPropertyChange(nameof(ShowFilterValue2));
                EventAggregator.PublishOnUIThread(new QueryBuilderUpdateEvent());
            }
        }

        public IEnumerable<FilterType> FilterTypes
        {
            get
            {
                foreach (FilterType ft in FilterType.GetValues(typeof(FilterType)))
                {
                    switch (ft) {
                        case FilterType.Is:
                        case FilterType.IsNot:
                        case FilterType.IsBlank:
                        case FilterType.IsNotBlank:
                            // the above filters apply to all data types
                            yield return ft;
                            break;
                        case FilterType.StartsWith:
                        case FilterType.DoesNotStartWith:
                        case FilterType.Contains:
                        case FilterType.DoesNotContain:
                            // these filters only apply to strings
                            if (TabularObject.SystemType == typeof(string)) yield return ft;
                            break;
                        case FilterType.In:
                        case FilterType.NotIn:
                            // if the data type is string and the model supports TREATAS
                            if (TabularObject.SystemType == typeof(string) && ( ModelCapabilities.DAXFunctions.TreatAs || ModelCapabilities.TableConstructor) ) yield return ft;
                            break;
                        case FilterType.GreaterThan:
                        case FilterType.GreaterThanOrEqual:
                        case FilterType.LessThan:
                        case FilterType.LessThanOrEqual:
                        case FilterType.Between:
                            // these filters only apply non-strings
                            if (TabularObject.SystemType != typeof(string)) yield return ft;
                            break;
                        default:
                            throw new NotSupportedException($"Unknown FilterType '{ft}'");

                    }
                }

                //var items = Enum.GetValues(typeof(FilterType)).Cast<FilterType>();
                //return items;
            }
        }

        private string _filterValue;
        [DataMember]
        public string FilterValue { get => _filterValue;
            set {
                _filterValue = value;
                NotifyOfPropertyChange();
                EventAggregator.PublishOnUIThread(new QueryBuilderUpdateEvent());
            } 
        }

        public bool ShowFilterValue
        {
            get { return FilterType != FilterType.IsBlank && FilterType != FilterType.IsNotBlank; }
        }

        private string _filterValue2;
        [DataMember]
        public string FilterValue2 { get => _filterValue2;
            set {
                _filterValue2 = value;
                NotifyOfPropertyChange();
                EventAggregator.PublishOnUIThread(new QueryBuilderUpdateEvent());
            }
        }
        public bool ShowFilterValue2 => FilterType == FilterType.Between;

    }
}
