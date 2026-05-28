using DaxStudio.UI.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Input;

namespace DaxStudio.Tests
{
    [TestClass]
    public class HotkeyBindingValidatorTests
    {
        [TestMethod]
        public void TryValidate_Allows_Control_Modified_Hotkey()
        {
            var isValid = HotkeyBindingValidator.TryValidate("Ctrl + C", out var message);

            Assert.IsTrue(isValid);
            Assert.AreEqual(string.Empty, message);
        }

        [TestMethod]
        public void TryValidate_Rejects_Unmodified_Letter()
        {
            var isValid = HotkeyBindingValidator.TryValidate("C", out var message);

            Assert.IsFalse(isValid);
            StringAssert.Contains(message, "single character");
        }

        [TestMethod]
        public void TryValidate_Rejects_Shift_Letter()
        {
            var isValid = HotkeyBindingValidator.TryValidate("Shift + A", out var message);

            Assert.IsFalse(isValid);
            StringAssert.Contains(message, "Cannot set a hotkey");
        }

        [TestMethod]
        public void TryValidate_Allows_Empty_Hotkey()
        {
            var isValid = HotkeyBindingValidator.TryValidate(string.Empty, out var message);

            Assert.IsTrue(isValid);
            Assert.AreEqual(string.Empty, message);
        }

        [TestMethod]
        public void IsSupportedForRegistration_Rejects_Unmodified_NonFunctionKey()
        {
            var isSupported = HotkeyBindingValidator.IsSupportedForRegistration(Key.C, ModifierKeys.None);

            Assert.IsFalse(isSupported);
        }

        [TestMethod]
        public void IsSupportedForRegistration_Allows_FunctionKey_Without_Modifier()
        {
            var isSupported = HotkeyBindingValidator.IsSupportedForRegistration(Key.F5, ModifierKeys.None);

            Assert.IsTrue(isSupported);
        }
    }
}
