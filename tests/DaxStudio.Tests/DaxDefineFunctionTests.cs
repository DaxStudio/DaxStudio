using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DaxDefineFunctionTests
    {
        private const string SECTION_START = "\r\n---- MODEL MEASURES BEGIN ----\r\n";

        [TestMethod]
        public void FunctionIsInsertedBeforeMeasures()
        {
            var section = SECTION_START + "MEASURE 'Sales'[Total] = SUM ( Sales[Amount] )\r\n";

            var result = DocumentViewModel.InsertFunctionIntoSection(section, "FUNCTION Multiply = (x: INT, y: INT) => x * y");

            Assert.AreEqual(
                SECTION_START + "FUNCTION Multiply = (x: INT, y: INT) => x * y\r\nMEASURE 'Sales'[Total] = SUM ( Sales[Amount] )\r\n",
                result);
        }

        [TestMethod]
        public void MultipleFunctionsKeepTheirOrder()
        {
            var section = SECTION_START + "MEASURE 'Sales'[Total] = SUM ( Sales[Amount] )\r\n";

            var result = DocumentViewModel.InsertFunctionIntoSection(section, "FUNCTION First = () => 1");
            result = DocumentViewModel.InsertFunctionIntoSection(result, "FUNCTION Second = () => 2");

            Assert.AreEqual(
                SECTION_START + "FUNCTION First = () => 1\r\nFUNCTION Second = () => 2\r\nMEASURE 'Sales'[Total] = SUM ( Sales[Amount] )\r\n",
                result);
        }

        [TestMethod]
        public void FunctionIsInsertedIntoSectionWithNoMeasures()
        {
            var section = SECTION_START;

            var result = DocumentViewModel.InsertFunctionIntoSection(section, "FUNCTION Multiply = (x: INT, y: INT) => x * y");

            Assert.AreEqual(SECTION_START + "FUNCTION Multiply = (x: INT, y: INT) => x * y\r\n", result);
        }

        [TestMethod]
        public void IndentedMeasuresAreDetected()
        {
            var section = SECTION_START + "    MEASURE 'Sales'[Total] = SUM ( Sales[Amount] )\r\n";

            var result = DocumentViewModel.InsertFunctionIntoSection(section, "FUNCTION Multiply = (x: INT) => x");

            Assert.AreEqual(
                SECTION_START + "FUNCTION Multiply = (x: INT) => x\r\n    MEASURE 'Sales'[Total] = SUM ( Sales[Amount] )\r\n",
                result);
        }

        [TestMethod]
        public void ExistingFunctionDefinitionIsDetected()
        {
            var query = "DEFINE\r\n---- MODEL MEASURES BEGIN ----\r\nFUNCTION Multiply = (x: INT, y: INT) => x * y\r\n---- MODEL MEASURES END ----\r\nEVALUATE ROW(\"a\",1)";

            Assert.IsTrue(DocumentViewModel.ContainsFunctionDefinition(query, "Multiply"));
        }

        [TestMethod]
        public void ExistingFunctionDefinitionIsDetectedWhenIndented()
        {
            var query = "DEFINE\r\n    FUNCTION  Sales.Multiply  = (x: INT) => x\r\nEVALUATE ROW(\"a\",1)";

            Assert.IsTrue(DocumentViewModel.ContainsFunctionDefinition(query, "Sales.Multiply"));
        }

        [TestMethod]
        public void OtherFunctionDefinitionsAreNotDetected()
        {
            var query = "DEFINE\r\nFUNCTION Multiply = (x: INT, y: INT) => x * y\r\nEVALUATE ROW(\"a\", Divide(1,2))";

            Assert.IsFalse(DocumentViewModel.ContainsFunctionDefinition(query, "Divide"));
        }

        [TestMethod]
        public void FunctionCallsAreNotTreatedAsDefinitions()
        {
            var query = "DEFINE\r\nMEASURE 'Sales'[Total] = Multiply ( 1, 2 )\r\nEVALUATE ROW(\"a\",1)";

            Assert.IsFalse(DocumentViewModel.ContainsFunctionDefinition(query, "Multiply"));
        }
    }
}
