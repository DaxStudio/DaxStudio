using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DaxStudio.Tests
{
    [TestClass]
    public class InputBindingCommandTests
    {
        [TestMethod]
        public void CanExecuteChanged_Subscribe_DoesNotThrow()
        {
            var command = new InputBindingCommand(() => { });
            EventHandler handler = (sender, args) => { };

            command.CanExecuteChanged += handler;
            command.CanExecuteChanged -= handler;
        }

        [TestMethod]
        public void ActionConstructor_ExecutesSuppliedDelegate()
        {
            var wasExecuted = false;
            var command = new InputBindingCommand(() => wasExecuted = true);

            command.Execute(null);

            Assert.IsTrue(wasExecuted);
        }
    }
}
