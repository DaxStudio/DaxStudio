using Caliburn.Micro;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Events;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
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
            SetDefaultFilterType();
        }

        private void SetDefaultFilterType()
        {
            if (TabularObject.SystemType != typeof(string)) FilterType = FilterType.Is;
            else FilterType = FilterType.Contains;
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
                            // TabularObject.DataType == typeof(string) &&
                            if (( ModelCapabilities.DAXFunctions.TreatAs || ModelCapabilities.TableConstructor) && !FilterValueIsParameter ) yield return ft;
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
                _filterValue = FilterValueIsParameter? value.Trim() : value;
                if (_filterValue == "@" || _filterValue == "@@")
                {
                    //IsNotifying = false;
                    FilterValueIsParameter = !FilterValueIsParameter;
                    //IsNotifying = true;
                    if (FilterValueIsParameter) _filterValue = _filterValue.TrimStart('@');
                }
                
                FilterValueValidationMessage = ValidateInput(FilterValue, FilterValueIsParameter);
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(FilterValueIsValid));
                NotifyOfPropertyChange(nameof(FilterValueValidationMessage));
                EventAggregator.PublishOnUIThread(new QueryBuilderUpdateEvent());
            } 
        }

        public bool FilterValueIsValid
        {
            get
            {
                return string.IsNullOrEmpty(FilterValueValidationMessage);
            }
        }

        bool _filterValueIsParameter = false;
        [DataMember]
        public bool FilterValueIsParameter { 
            get => _filterValueIsParameter; 
            set {
                
                if (_filterValueIsParameter && !value && !FilterValue.StartsWith("@")) { FilterValue = "@" + FilterValue; }
                _filterValueIsParameter = value;
                NotifyOfPropertyChange();
                if (FilterType == FilterType.In) FilterType = FilterType.Is;
                if (FilterType == FilterType.NotIn) FilterType = FilterType.IsNot;
                NotifyOfPropertyChange(nameof(FilterTypes));
                EventAggregator.PublishOnUIThread(new QueryBuilderUpdateEvent());
            } 
        }

        private string ValidateInput(string input, bool isParameter)
        {
            if (isParameter) return string.Empty;

                if (FilterType == FilterType.In || FilterType == FilterType.NotIn)
                {
                    foreach (var line in input.Split('\n'))
                    {
                        try { 
                            ValidateInputValue(line);
                        } catch (Exception ex)
                        {
                            return $"Unable to parse '{line}' as {TabularObject.SystemType.Name}\n{ex.Message}";
                        }
                    }
                }
                else
                {
                    try
                    {
                        ValidateInputValue(input);
                    }
                    catch (Exception ex)
                    {
                        return $"Unable to parse '{input}' as {TabularObject.SystemType.Name}\n{ex.Message}";
                    }
                }

                return string.Empty;

        }

        private void ValidateInputValue(string input)
        {
            if (TabularObject.SystemType == typeof(DateTime)) { DateTime _ = DateTime.Parse(input); }
            if (TabularObject.SystemType == typeof(Int64)) { var _ = Int64.Parse(input); }
            if (TabularObject.SystemType == typeof(Decimal)) { var _ = Decimal.Parse(input); }
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
                
                _filterValue2 = FilterValue2IsParameter ? value.Trim() : value;
                if (_filterValue2.StartsWith("@"))
                {
                    //IsNotifying = false;
                    FilterValue2IsParameter = !FilterValue2IsParameter;
                    //IsNotifying = true;
                    if (FilterValue2IsParameter) _filterValue2 = _filterValue2.TrimStart('@');
                }

                FilterValue2ValidationMessage = ValidateInput(FilterValue2, FilterValue2IsParameter);
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(FilterValue2IsValid));
                NotifyOfPropertyChange(nameof(FilterValue2ValidationMessage));
                EventAggregator.PublishOnUIThread(new QueryBuilderUpdateEvent());
            }
        }

        public bool FilterValue2IsValid
        {
            get { return string.IsNullOrEmpty(FilterValue2ValidationMessage); }
        }

        bool _filterValue2IsParameter = false;
        [DataMember]
        public bool FilterValue2IsParameter
        {
            get => _filterValue2IsParameter;
            set
            {

                if (_filterValue2IsParameter && !value && !FilterValue2.StartsWith("@")) { FilterValue2 = "@" + FilterValue2; }
                _filterValue2IsParameter = value;
                NotifyOfPropertyChange();
                EventAggregator.PublishOnUIThread(new QueryBuilderUpdateEvent());
            }
        }

        public bool ShowFilterValue2 => FilterType == FilterType.Between;

        public string FilterValueValidationMessage { get; private set; }
        public string FilterValue2ValidationMessage { get; private set; }
    }
}
