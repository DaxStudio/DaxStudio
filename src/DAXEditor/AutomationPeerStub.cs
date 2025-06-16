using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;

namespace DAXEditorControl
{
    internal sealed class AutomationPeerStub : FrameworkElementAutomationPeer
    {
        AutomationControlType _controlType;

        public AutomationPeerStub(FrameworkElement owner, AutomationControlType controlType)
            : base(owner)
        {
            _controlType = controlType;
        }

        protected override string GetNameCore()
        {
            return "AutomationPeerStub";
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return _controlType;
        }

        protected override List<AutomationPeer> GetChildrenCore()
        {
            return new List<AutomationPeer>();
        }
    }
}
