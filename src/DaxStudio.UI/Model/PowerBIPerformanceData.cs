﻿using DaxStudio.UI.Interfaces;
using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaxStudio.UI.Model
{
    public class PowerBIPerformanceData : IQueryTextProvider
    {
        public int Sequence { get; set; }
        public string Id { get; set; }
        public string ParentId { get; set; }
        public string Component { get; set; }
        public string Name { get; set; }

        public string VisualName { get; set; }
        public string QueryText { get; set; }
        public long RowCount { get; set; }
        public bool? Error { get; set; }
        public DateTime QueryStartTime { get; set; }
        public DateTime? QueryEndTime { get; set; }
        public DateTime RenderStartTime { get; set; }
        public DateTime? RenderEndTime { get; set; }

        
        public double QueryDuration { get {
                if (!QueryEndTime.HasValue) return -1;
                TimeSpan duration = QueryEndTime.Value - QueryStartTime;
                return duration.TotalMilliseconds;
            }
        }
        public double RenderDuration
        {
            get
            {
                if (!RenderEndTime.HasValue) return -1;
                TimeSpan duration = RenderEndTime.Value - RenderStartTime;
                return duration.TotalMilliseconds;
            }
        }
        public double TotalDuration => (QueryEndTime.HasValue && RenderEndTime.HasValue) ? QueryDuration + RenderDuration : -1;

        // copy/paste from the data grid converts the data to tab delimited format
        // so we replace any embedded tabs with 4 spaces 
        public string QueryTextQuoted { 
            get { 
                return QueryText.Replace("\t","    ");
            } 
        }

        #region IQueryTextProvider 

        string IQueryTextProvider.EditorText => this.QueryText;

        string IQueryTextProvider.QueryText => this.QueryText;

        List<AdomdParameter> IQueryTextProvider.ParameterCollection { get; } = new List<AdomdParameter>();

        QueryInfo IQueryTextProvider.QueryInfo { get ; set; }

        #endregion
    }
}
