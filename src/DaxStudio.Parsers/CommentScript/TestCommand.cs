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
        public TestCommand(string testName) {
            TestName = testName;
        }
        public string TestName { get; set; }
    }
}
