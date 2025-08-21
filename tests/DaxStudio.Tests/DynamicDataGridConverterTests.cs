using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DynamicDataGridConverterTests
    {
        [TestMethod]
        public void TestBindingPaths()
        {
            // Arrange
            var converter = new DaxStudio.UI.Converters.DynamicDataGridConverter();
            var methodInfo = typeof(DaxStudio.UI.Converters.DynamicDataGridConverter).GetMethod("FixBindingPath", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.IsNotNull(methodInfo, "Could not find the private FixBindingPath method via reflection");
            
            var testCases = new List<(string input, string expected)>
            {
                ("[Column1]", "[^[Column1^]]"),
                ("[Column1^]", "[^[Column1^^^]]"),
                ("Column&1}^", "[Column^&1^}^^]")
            };
            // Act & Assert
            foreach (var testCase in testCases)
            {
                var result = (string)methodInfo.Invoke(converter, new object[] { testCase.input });
                Assert.AreEqual(testCase.expected, result, $"Failed for input: {testCase.input}");
            }
        }

    }
}
