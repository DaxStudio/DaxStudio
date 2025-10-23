using Microsoft.VisualStudio.TestTools.UnitTesting;
using DaxStudio.CommandLine.Commands;

namespace DaxStudio.CommandLine.Tests
{
    [TestClass]
    public class CommandLineParameterTests
    {
        [TestMethod]
        public void TestServerDatabaseNames()
        {
            var settings = new FileCommand.Settings();
            settings.Server = "localhost";
            settings.Database = "Adventure Works";

            var validationResult = settings.Validate();
            Assert.IsTrue(validationResult.Successful, "Validation result should be successful");
        }

        [TestMethod]
        public void Using_only_servername_should_fail()
        {
            var settings = new FileCommand.Settings();
            settings.Server = "localhost";


            var validationResult = settings.Validate();
            Assert.IsFalse(validationResult.Successful, validationResult.Message);
            Assert.AreEqual("You must specify a <database> when using the <server> parameter and not connecting to a .pbix/.pbip file", validationResult.Message);
        }

        [TestMethod]
        public void Using_only_databasename_should_fail()
        {
            var settings = new FileCommand.Settings();
            settings.Database = "Adventure Works";


            var validationResult = settings.Validate();
            Assert.IsFalse(validationResult.Successful, validationResult.Message);
            Assert.AreEqual("You must specify a <server> when using the <database> parameter", validationResult.Message);
        }

        [TestMethod]
        public void Using_only_servername_with_connectionstring_should_fail()
        {
            var settings = new FileCommand.Settings();
            settings.Server = "localhost";
            settings.ConnectionString = "data source=localhost";

            var validationResult = settings.Validate();
            Assert.IsFalse(validationResult.Successful, validationResult.Message);
            Assert.AreEqual("You cannot specify a <Server> or <Database> when passing a <ConnectionString>", validationResult.Message);
        }

        [TestMethod]
        public void Using_only_connectionstring_should_suceed()
        {
            var settings = new FileCommand.Settings();
            settings.ConnectionString = "data source=localhost";

            var validationResult = settings.Validate();
            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.IsNull(validationResult.Message);
        }

        [TestMethod]
        public void Using_connectionstring_and_user_should_succeed()
        {
            var settings = new FileCommand.Settings();
            settings.ConnectionString = "data source=localhost";
            settings.UserID = "testUser";
            settings.Password = "testPwd";

            var validationResult = settings.Validate();
            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.IsNull(validationResult.Message);
            Assert.AreEqual("Data Source=localhost;User ID=testUser;Password=testPwd", settings.FullConnectionString, "connection strings don't match");
        }

        [TestMethod]
        public void access_token_with_server_should_succeed()
        {
            var settings = new AccessTokenCommand.Settings();
            settings.Server = "asazure://australiasoutheast.asazure.windows.net/myserver";
            settings.Database = "mydatabase";
            var validationResult = settings.Validate();
            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.IsNull(validationResult.Message);
        }

        [TestMethod]
        public void Using_server_and_password_should_succeed()
        {
            var settings = new FileCommand.Settings();
            settings.Server = "localhost";
            settings.Database = "Adventure Works";
            settings.Password = "testPwd";


            var validationResult = settings.Validate();
            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.IsNull(validationResult.Message);
        }

        [TestMethod]
        public void access_token_command_validation_should_succeed()
        {
            var settings = new AccessTokenCommand.Settings();
            settings.Server = "asazure://australiasoutheast.asazure.windows.net/myserver";
            settings.Database = "mydatabase";
            var validationResult = settings.Validate();
            var accessTokenCommand = new AccessTokenCommand();
            var cmdValidationResult = accessTokenCommand.Validate(null , settings);
            Assert.IsTrue(validationResult.Successful, validationResult.Message);
            Assert.IsTrue(cmdValidationResult.Successful, cmdValidationResult.Message);
            Assert.IsNull(validationResult.Message);
        }

    }
}

