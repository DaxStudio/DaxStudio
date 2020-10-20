using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows.Controls;

namespace DaxStudio.Controls.DataGridFilter.Support
{
    [Serializable()]
    public class FilterData : INotifyPropertyChanged
    {
        #region Metadata

        public FilterType Type { get; set; }
        public String ValuePropertyBindingPath { get; set; }
        public Type ValuePropertyType { get; set; }
        public bool IsTypeInitialized { get; set; }
        public bool IsCaseSensitiveSearch { get; set; }
        public bool IsContainsTextSearch { get; set; }
        //query optimization fileds
        public bool IsSearchPerformed { get; set; }
        public bool IsRefresh { get; set; }
        //query optimization fileds
        #endregion

        #region Filter Change Notification
        public event EventHandler<EventArgs> FilterChangedEvent;
        public event EventHandler<EventArgs> FilterClearedEvent;
        private bool isClearData;

        private void OnFilterChangedEvent()
        {
            EventHandler<EventArgs> temp = FilterChangedEvent;

            if (temp != null)
            {
                bool filterChanged = false;

                switch (Type)
                {
                    case FilterType.Numeric:
                    case FilterType.DateTime:

                        filterChanged = (Operator != FilterOperator.Undefined || !string.IsNullOrEmpty(QueryString));
                        break;

                    case FilterType.NumericBetween:
                    case FilterType.DateTimeBetween:

                        _operator = FilterOperator.Between;
                        filterChanged = true;
                        break;

                    case FilterType.Text:

                        _operator = !IsContainsTextSearch? FilterOperator.Like: FilterOperator.Contains;
                        filterChanged = true;
                        break;

                    case FilterType.List:
                    case FilterType.Boolean:

                        _operator = FilterOperator.Equals;
                        filterChanged = true;
                        break;

                    default:
                        filterChanged = false;
                        break;
                }

                if (filterChanged && !isClearData) temp(this, EventArgs.Empty);
            }
        }

        private void OnFilterClearedEvent()
        {
            EventHandler<EventArgs> temp = FilterClearedEvent;

            if (temp != null)
            {
                temp(this, EventArgs.Empty);
            }
        }
        #endregion
        public void ClearData()
        {
            isClearData = true;

            Operator           = FilterOperator.Undefined;
            if (!string.IsNullOrEmpty(QueryString)) QueryString = null;
            if (!string.IsNullOrEmpty(QueryStringTo)) QueryStringTo = null;
            OnFilterClearedEvent();

            isClearData = false;
        }

        private FilterOperator _operator;
        public FilterOperator Operator
        {
            get { return _operator; }
            set
            {
                if(_operator != value)
                {
                    _operator = value;
                    NotifyPropertyChanged(nameof(Operator));
                    OnFilterChangedEvent();
                }
            }
        }

        private string queryString;
        public string QueryString
        {
            get { return queryString; }
            set
            {
                if (queryString != value)
                {
                    queryString = value;
                    
                    if (queryString == null) queryString = String.Empty;

                    NotifyPropertyChanged(nameof(QueryString));
                    OnFilterChangedEvent();
                }
            }
        }

        private string queryStringTo;
        public string QueryStringTo
        {
            get { return queryStringTo; }
            set
            {
                if (queryStringTo != value)
                {
                    queryStringTo = value;
                    
                    if (queryStringTo == null) queryStringTo = String.Empty;

                    NotifyPropertyChanged(nameof(QueryStringTo));
                    OnFilterChangedEvent();
                }
            }
        }

        public FilterData(
            FilterOperator Operator,
            FilterType Type,
            String ValuePropertyBindingPath,
            Type ValuePropertyType,
            String QueryString,
            String QueryStringTo,
            bool IsTypeInitialized,
            bool IsCaseSensitiveSearch,
            bool IsContainsTextSearch
            )
        {
            
            this.Operator = Operator;
            this.Type = Type;
            this.ValuePropertyBindingPath = ValuePropertyBindingPath;
            this.ValuePropertyType = ValuePropertyType;
            this.QueryString   = QueryString;
            this.QueryStringTo = QueryStringTo;

            this.IsTypeInitialized    = IsTypeInitialized;
            this.IsCaseSensitiveSearch = IsCaseSensitiveSearch;
            this.IsContainsTextSearch = IsContainsTextSearch;
            
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
