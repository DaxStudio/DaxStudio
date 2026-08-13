using System;
using System.Collections.Specialized;
using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class CmdLineArgsTests
    {
        private static CmdLineArgs NewArgs() => new CmdLineArgs(new HybridDictionary());

        [TestMethod]
        public void Parse_PortShortForm_SetsPort()
        {
            var args = NewArgs();
            args.Parse(new[] { "-p", "9999" });
            Assert.AreEqual(9999, args.Port);
        }

        [TestMethod]
        public void Parse_PortLongForm_SetsPort()
        {
            var args = NewArgs();
            args.Parse(new[] { "--port", "9999" });
            Assert.AreEqual(9999, args.Port);
        }

        [TestMethod]
        public void Parse_PortMissing_DefaultsToZero()
        {
            var args = NewArgs();
            args.Parse(Array.Empty<string>());
            Assert.AreEqual(0, args.Port);
        }

        [TestMethod]
        public void Parse_LogShortForm_EnablesLogging()
        {
            var args = NewArgs();
            args.Parse(new[] { "-l" });
            Assert.IsTrue(args.LoggingEnabledByCommandLine);
            Assert.IsTrue(args.LoggingEnabled);
        }

        [TestMethod]
        public void Parse_LogLongForm_EnablesLogging()
        {
            var args = NewArgs();
            args.Parse(new[] { "--log" });
            Assert.IsTrue(args.LoggingEnabledByCommandLine);
            Assert.IsTrue(args.LoggingEnabled);
        }

        [TestMethod]
        public void Parse_LogExplicitTrue_EnablesLogging()
        {
            var args = NewArgs();
            args.Parse(new[] { "--log=true" });
            Assert.IsTrue(args.LoggingEnabledByCommandLine);
        }

        [TestMethod]
        public void Parse_LogExplicitFalse_DisablesLogging()
        {
            var args = NewArgs();
            args.Parse(new[] { "--log=false" });
            Assert.IsFalse(args.LoggingEnabledByCommandLine);
        }

        [TestMethod]
        public void Parse_LogMissing_DefaultsToFalse()
        {
            var args = NewArgs();
            args.Parse(Array.Empty<string>());
            Assert.IsFalse(args.LoggingEnabledByCommandLine);
            Assert.IsFalse(args.LoggingEnabled);
        }

        [TestMethod]
        public void Parse_FileShortForm_SetsFileName()
        {
            var args = NewArgs();
            args.Parse(new[] { "-f", "myfile.dax" });
            Assert.AreEqual("myfile.dax", args.FileName);
        }

        [TestMethod]
        public void Parse_FileLongForm_SetsFileName()
        {
            var args = NewArgs();
            args.Parse(new[] { "--file", "myfile.dax" });
            Assert.AreEqual("myfile.dax", args.FileName);
        }

        [TestMethod]
        public void Parse_FileSingleDashLongForm_SetsFileName()
        {
            // legacy syntax supported prior to the Spectre.Console.Cli migration
            var args = NewArgs();
            args.Parse(new[] { "-file", "myfile.dax" });
            Assert.AreEqual("myfile.dax", args.FileName);
        }

        [TestMethod]
        [DataRow("-file")]
        [DataRow("-FILE")]
        [DataRow("--file")]
        [DataRow("/file")]
        [DataRow("-f")]
        public void Parse_FileVariants_PreserveFilePathCasing(string token)
        {
            var args = NewArgs();
            args.Parse(new[] { token, @"C:\Temp\My File.DAX" });
            Assert.AreEqual(@"C:\Temp\My File.DAX", args.FileName, $"FileName was not set for token '{token}'");
        }

        [TestMethod]
        public void Parse_FileSingleDashLongFormWithEquals_PreservesPathCasing()
        {
            var args = NewArgs();
            args.Parse(new[] { @"-file=C:\Temp\My File.DAX" });
            Assert.AreEqual(@"C:\Temp\My File.DAX", args.FileName);
        }

        [TestMethod]
        public void Parse_ServerSingleDashLongForm_SetsServer()
        {
            var args = NewArgs();
            args.Parse(new[] { "-server", "localhost" });
            Assert.AreEqual("localhost", args.Server);
        }

        [TestMethod]
        public void Parse_UriWithEquals_PreservesBase64Casing()
        {
            var args = NewArgs();
            const string dax = "EVALUATE Customer";
            var encoded = dax.Base64Encode();

            args.Parse(new[] { $"--uri=daxstudio://launch/?Query={Uri.EscapeDataString(encoded)}" });

            Assert.IsTrue(args.FromUri);
            Assert.AreEqual(dax, args.Query);
        }

        [TestMethod]
        public void Parse_ServerShortForm_SetsServer()
        {
            var args = NewArgs();
            args.Parse(new[] { "-s", "localhost" });
            Assert.AreEqual("localhost", args.Server);
        }

        [TestMethod]
        public void Parse_ServerLongForm_SetsServer()
        {
            var args = NewArgs();
            args.Parse(new[] { "--server", "localhost" });
            Assert.AreEqual("localhost", args.Server);
        }

        [TestMethod]
        public void Parse_ServerLongFormUppercase_SetsServer()
        {
            var args = NewArgs();
            args.Parse(new[] { "--SERVER", "localhost" });
            Assert.AreEqual("localhost", args.Server);
        }

        [TestMethod]
        public void Parse_DatabaseShortForm_SetsDatabase()
        {
            var args = NewArgs();
            args.Parse(new[] { "-d", "AdventureWorks" });
            Assert.AreEqual("AdventureWorks", args.Database);
        }

        [TestMethod]
        public void Parse_DatabaseLongForm_SetsDatabase()
        {
            var args = NewArgs();
            args.Parse(new[] { "--database", "AdventureWorks" });
            Assert.AreEqual("AdventureWorks", args.Database);
        }

        [TestMethod]
        public void Parse_ResetShortForm_SetsReset()
        {
            var args = NewArgs();
            args.Parse(new[] { "-r" });
            Assert.IsTrue(args.Reset);
        }

        [TestMethod]
        public void Parse_ResetLongForm_SetsReset()
        {
            var args = NewArgs();
            args.Parse(new[] { "--reset" });
            Assert.IsTrue(args.Reset);
        }

        [TestMethod]
        public void Parse_NoPreview_SetsNoPreview()
        {
            var args = NewArgs();
            args.Parse(new[] { "--nopreview" });
            Assert.IsTrue(args.NoPreview);
        }

        [TestMethod]
        public void Parse_NoPreviewMissing_DefaultsToFalse()
        {
            var args = NewArgs();
            args.Parse(Array.Empty<string>());
            Assert.IsFalse(args.NoPreview);
        }

        [TestMethod]
        public void Parse_HelpShortForm_SetsShowHelp()
        {
            var args = NewArgs();
            args.Parse(new[] { "-?" });
            Assert.IsTrue(args.ShowHelp);
        }

        [TestMethod]
        public void Parse_HelpLongForm_SetsShowHelp()
        {
            var args = NewArgs();
            args.Parse(new[] { "--help" });
            Assert.IsTrue(args.ShowHelp);
        }

        [TestMethod]
        public void Parse_UriShortForm_PopulatesArgsFromUri()
        {
            var args = NewArgs();
            args.Parse(new[] { "-u", "daxstudio://launch/?Server=localhost&Database=AdventureWorks" });
            Assert.IsTrue(args.FromUri);
            Assert.AreEqual("localhost", args.Server);
            Assert.AreEqual("AdventureWorks", args.Database);
        }

        [TestMethod]
        public void Parse_UriLongForm_PopulatesArgsFromUri()
        {
            var args = NewArgs();
            args.Parse(new[] { "--uri", "daxstudio://launch/?Server=localhost&Database=AdventureWorks" });
            Assert.IsTrue(args.FromUri);
            Assert.AreEqual("localhost", args.Server);
            Assert.AreEqual("AdventureWorks", args.Database);
        }

        [TestMethod]
        public void Parse_MultipleOptions_SetsAll()
        {
            var args = NewArgs();
            args.Parse(new[]
            {
                "-p", "1234",
                "-s", "localhost",
                "-d", "AdventureWorks",
                "-f", "myfile.dax",
                "-l"
            });

            Assert.AreEqual(1234, args.Port);
            Assert.AreEqual("localhost", args.Server);
            Assert.AreEqual("AdventureWorks", args.Database);
            Assert.AreEqual("myfile.dax", args.FileName);
            Assert.IsTrue(args.LoggingEnabledByCommandLine);
        }

        [TestMethod]
        public void Parse_DosStyleLongOption_SetsServer()
        {
            var args = NewArgs();
            args.Parse(new[] { "/server", "localhost" });
            Assert.AreEqual("localhost", args.Server);
        }

        [TestMethod]
        public void Parse_DosStyleLongOptionMixedCase_SetsServer()
        {
            var args = NewArgs();
            args.Parse(new[] { "/SeRvEr", "localhost" });
            Assert.AreEqual("localhost", args.Server);
        }

        [TestMethod]
        public void Parse_DosStyleLongOptionWithEquals_SetsDatabase()
        {
            var args = NewArgs();
            args.Parse(new[] { "/database=AdventureWorks" });
            Assert.AreEqual("AdventureWorks", args.Database);
        }

        [TestMethod]
        public void Parse_DosStyleShortOption_SetsServer()
        {
            var args = NewArgs();
            args.Parse(new[] { "/s", "localhost" });
            Assert.AreEqual("localhost", args.Server);
        }

        [TestMethod]
        public void Parse_DosStyleMultipleOptions_SetsAll()
        {
            var args = NewArgs();
            args.Parse(new[]
            {
                "/server", "localhost",
                "/database", "AdventureWorks",
                "/port", "1234"
            });

            Assert.AreEqual("localhost", args.Server);
            Assert.AreEqual("AdventureWorks", args.Database);
            Assert.AreEqual(1234, args.Port);
        }

        [TestMethod]
        public void Parse_DosStyleBoolFlag_EnablesLogging()
        {
            var args = NewArgs();
            args.Parse(new[] { "/log" });
            Assert.IsTrue(args.LoggingEnabledByCommandLine);
        }

        [TestMethod]
        public void Parse_DosStyleHelp_SetsShowHelp()
        {
            var args = NewArgs();
            args.Parse(new[] { "/?" });
            Assert.IsTrue(args.ShowHelp);
        }

        [TestMethod]
        [DataRow("-?")]
        [DataRow("/?")]
        [DataRow("--?")]
        [DataRow("-h")]
        [DataRow("/h")]
        [DataRow("--h")]
        [DataRow("-H")]
        [DataRow("-help")]
        [DataRow("/help")]
        [DataRow("/Help")]
        [DataRow("/HELP")]
        [DataRow("--help")]
        [DataRow("--Help")]
        public void Parse_HelpVariants_SetShowHelp(string token)
        {
            var args = NewArgs();
            args.Parse(new[] { token });
            Assert.IsTrue(args.ShowHelp, $"ShowHelp was not set for token '{token}'");
        }

        [TestMethod]
        public void Parse_ForwardSlashFilePath_IsNotMistakenForOption()
        {
            // A leading-slash token that isn't a recognised option name (eg a
            // unix-style file path) must pass through to Spectre unchanged.
            var args = NewArgs();
            args.Parse(new[] { "-f", "/temp/myfile.dax" });
            Assert.AreEqual("/temp/myfile.dax", args.FileName);
        }

        [TestMethod]
        public void ParseUri_PopulatesServerAndDatabase()
        {
            var args = NewArgs();
            args.ParseUri("daxstudio://launch/?Server=localhost&Database=AdventureWorks");

            Assert.IsTrue(args.FromUri);
            Assert.AreEqual("localhost", args.Server);
            Assert.AreEqual("AdventureWorks", args.Database);
        }

        [TestMethod]
        public void ParseUri_QueryIsBase64Decoded()
        {
            var args = NewArgs();
            const string dax = "EVALUATE Customer";
            var encoded = dax.Base64Encode();

            args.ParseUri($"daxstudio://launch/?Query={Uri.EscapeDataString(encoded)}");

            Assert.IsTrue(args.FromUri);
            Assert.AreEqual(dax, args.Query);
        }

        [TestMethod]
        public void ParseUri_UnknownQueryParameters_AreIgnored()
        {
            var args = NewArgs();
            args.ParseUri("daxstudio://launch/?NotARealOption=foo&Server=srv");

            Assert.IsTrue(args.FromUri);
            Assert.AreEqual("srv", args.Server);
        }
    }
}
