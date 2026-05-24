using System.ComponentModel.Composition;
using System.Runtime.Serialization;
using Caliburn.Micro;
using DaxStudio.Core.Options;
using DaxStudio.Core.Utils;
using DaxStudio.Interfaces;
using DaxStudio.UI.Utils;

namespace DaxStudio.UI.ViewModels
{
    /// <summary>
    /// WPF/Caliburn host for the UI-free <see cref="OptionsModel"/>. The naming
    /// preserves the Caliburn.Micro view convention that binds
    /// <c>Views\OptionsView.xaml</c> to <c>OptionsViewModel</c>. The base class
    /// in <see cref="DaxStudio.Core.Options"/> owns all option properties,
    /// persistence and event publishing; this subclass adds MEF discovery
    /// for the WPF shell and overrides UI-coupled hooks (proxy reset, app focus).
    /// </summary>
    [DataContract]
    [Export(typeof(IGlobalOptions))]
    public sealed class OptionsViewModel : OptionsModel
    {
        [ImportingConstructor]
        public OptionsViewModel(IEventAggregator eventAggregator, ISettingProvider settingProvider)
            : base(eventAggregator, settingProvider)
        {
        }

        protected override void OnProxySettingsChanged()
        {
            HttpClientHelper.ResetProxy();
        }

        protected override bool IsApplicationActive()
        {
            return ApplicationHelper.IsApplicationActive();
        }
    }
}
