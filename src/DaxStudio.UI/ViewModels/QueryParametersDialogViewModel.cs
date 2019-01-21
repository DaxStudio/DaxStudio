using Caliburn.Micro;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;

namespace DaxStudio.UI.ViewModels
{
    [Export]
    public class QueryParametersDialogViewModel:Screen
    {
        private readonly QueryInfo _queryInfo;
        private readonly DocumentViewModel _document;
        [ImportingConstructor]
        public QueryParametersDialogViewModel(DocumentViewModel document, QueryInfo queryInfo)
        {
            _document = document;
            _queryInfo = queryInfo;
            Parameters = new ObservableCollection<QueryParameter>(queryInfo.Parameters.Values);
        }

        public ObservableCollection<QueryParameter> Parameters { get; private set; }

        public DialogResult DialogResult { get; set; }

        #region button click handlers

        public void WriteParameterXml()
        {
            ParameterHelper.WriteParameterXml(_document, _queryInfo);
        }

        public void Ok()
        {
            // TODO - copy parameters
            //foreach ( var p in Parameters)
            //{
            //    _queryInfo.Parameters[p.Name].Value = p.Value;
            //}
            DialogResult = DialogResult.OK;
            TryClose();
        }

        public void Cancel()
        {
            DialogResult = DialogResult.Cancel;
            TryClose();
        }

        #endregion
    }
}
