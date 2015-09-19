using Caliburn.Micro;
using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaxStudio.UI.Utils;
using System.Security;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(IGlobalOptions))]
    [Export(typeof(OptionsViewModel))]
    public class OptionsViewModel:Screen, IGlobalOptions
    {
        private string _selectedFontFamily;
        private bool _showLineNumbers;
        private bool _enableIntellisense;
        private double _fontSize;
        private bool _proxyUseSystem;
        private string _proxyAddress;
        private string _proxyUser;
        private string _proxyPassword;
        private SecureString _proxySecurePassword;
        private int _maxQueryHistory;
        private IEventAggregator _eventAggregator;

        [ImportingConstructor]
        public OptionsViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            EditorFontFamily = RegistryHelper.GetValue<string>("EditorFontFamily","Lucida Console"); 
            EditorFontSize = RegistryHelper.GetValue<double>("EditorFontSize", 11);
            EditorShowLineNumbers = RegistryHelper.GetValue<bool>("EditorShowLineNumbers",true);
            EditorEnableIntellisense = RegistryHelper.GetValue<bool>("EditorEnableIntellisense", true);
            ProxyUseSystem = RegistryHelper.GetValue<bool>("ProxyUseSystem", true);
            ProxyAddress = RegistryHelper.GetValue<string>("ProxyAddress", "");
            ProxyUser = RegistryHelper.GetValue<string>("ProxyUser", "");
            ProxyPassword = RegistryHelper.GetValue<string>("ProxyPassword", "").Decrypt();
            MaxQueryHistory = RegistryHelper.GetValue<int>("MaxQueryHistory", 200);
        }

        public string EditorFontFamily { get { return _selectedFontFamily; } 
            set{
                if (_selectedFontFamily == value) return;
                _selectedFontFamily = value;
                NotifyOfPropertyChange(() => EditorFontFamily);
                _eventAggregator.PublishOnUIThread(new Events.UpdateEditorOptions());
                RegistryHelper.SetValueAsync<string>("EditorFontFamily", value);
            } 
        }
        public double EditorFontSize { get { return _fontSize; } 
            set {
                if (_fontSize == value) return;
                _fontSize = value;
                NotifyOfPropertyChange(() => EditorFontSize);
                _eventAggregator.PublishOnUIThread(new Events.UpdateEditorOptions());
                RegistryHelper.SetValueAsync<double>("EditorFontSize", value);
            } 
        }
        public bool EditorShowLineNumbers { get { return _showLineNumbers; }
            set
            {
                if (_showLineNumbers == value) return;
                _showLineNumbers = value;
                NotifyOfPropertyChange(() => EditorShowLineNumbers);
                _eventAggregator.PublishOnUIThread(new Events.UpdateEditorOptions());
                RegistryHelper.SetValueAsync<bool>("EditorShowLineNumbers", value);
            }
        }
        public bool EditorEnableIntellisense
        {
            get { return _enableIntellisense; }
            set
            {
                if (_enableIntellisense == value) return;
                _enableIntellisense = value;
                NotifyOfPropertyChange(() => EditorEnableIntellisense);
                _eventAggregator.PublishOnUIThread(new Events.UpdateEditorOptions());
                RegistryHelper.SetValueAsync<bool>("EditorEnableIntellisense", value);
            }
        }

        #region Http Proxy properties

        public bool ProxyUseSystem
        {
            get { return _proxyUseSystem; }
            set
            {
                if (_proxyUseSystem == value) return;
                _proxyUseSystem = value;
                NotifyOfPropertyChange(() => ProxyUseSystem);
                _eventAggregator.PublishOnUIThread(new Events.UpdateEditorOptions());
                RegistryHelper.SetValueAsync<bool>("ProxyUseSystem", value);
            }
        }

        public string ProxyAddress
        {
            get { return _proxyAddress; }
            set
            {
                if (_proxyAddress == value) return;
                _proxyAddress = value;
                NotifyOfPropertyChange(() => ProxyAddress);
                _eventAggregator.PublishOnUIThread(new Events.UpdateEditorOptions());
                RegistryHelper.SetValueAsync<string>("ProxyAddress", value);
            }
        }

        public string ProxyUser
        {
            get { return _proxyUser; }
            set
            {
                if (_proxyUser == value) return;
                _proxyUser = value;
                NotifyOfPropertyChange(() => ProxyUser);
                _eventAggregator.PublishOnUIThread(new Events.UpdateEditorOptions());
                RegistryHelper.SetValueAsync<string>("ProxyUser", value);
            }
        }

        public string ProxyPassword
        {
            get { return _proxyPassword; }
            set
            {
                if (_proxyPassword == value) return;
                _proxyPassword = value;
                NotifyOfPropertyChange(() => ProxyPassword);
                _eventAggregator.PublishOnUIThread(new Events.UpdateEditorOptions());
                RegistryHelper.SetValueAsync<string>("ProxyPassword", value.Encrypt());
            }
        }

        public SecureString ProxySecurePassword
        {
            get { return _proxySecurePassword; }
            set
            {
                if (_proxySecurePassword == value) return;
                _proxySecurePassword = value;
                NotifyOfPropertyChange(() => ProxyPassword);
                _eventAggregator.PublishOnUIThread(new Events.UpdateEditorOptions());
                RegistryHelper.SetValueAsync<string>("ProxyPassword", value.GetInsecureString().Encrypt());
            }
        }

        #endregion




        public int MaxQueryHistory { get { return _maxQueryHistory; }
            set
            {
                if (_maxQueryHistory == value) return;
                _maxQueryHistory = value;
                NotifyOfPropertyChange(() => MaxQueryHistory);
                _eventAggregator.PublishOnUIThread(new Events.UpdateEditorOptions());
                RegistryHelper.SetValueAsync<int>("MaxQueryHistory", value);
            }

        }
    }
}
