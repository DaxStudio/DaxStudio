using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class TestCommand: ScriptCommand
    {
        public TestCommand(string testType, string testName) {
            TestType = testType;
            TestName = testName;
        }
        public string TestType { get; set; }
        public string TestName { get; set; }
    }
}
