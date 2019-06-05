using DaxStudio.UI.Extensions;
using DaxStudio.UI.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class TraceEventTests
    {
        [TestMethod]
        public void ParsePropertiesTest()
        {
            var props = @"<PropertyList xmlns=""urn:schemas-microsoft-com:xml-analysis"">
          <Catalog>Adventure Works</Catalog>
          <EffectiveUserName>mydomain\testuser</EffectiveUserName>
          <LocaleIdentifier>1033</LocaleIdentifier>
          <Format>Tabular</Format>
          <Content>SchemaData</Content>
          <Timeout>0</Timeout>
          <ReturnCellProperties>true</ReturnCellProperties>
          <DbpropMsmdFlattened2>true</DbpropMsmdFlattened2>
          <DbpropMsmdActivityID>8ac8460e-281b-4d4e-ba2a-23e3ace266da</DbpropMsmdActivityID>
          <DbpropMsmdCurrentActivityID>8ac8460e-281b-4d4e-ba2a-23e3ace266da</DbpropMsmdCurrentActivityID>
          <DbpropMsmdRequestID>1c71dde3-34e5-42f9-b1eb-ed951d6a3041</DbpropMsmdRequestID>
        </PropertyList>";
            QueryBeginEvent beginEvent = new QueryBeginEvent();
            beginEvent.RequestProperties = props;
            var effUser = beginEvent.ParseEffectiveUsername();
            Assert.AreEqual("mydomain\\testuser", effUser);

        }

        [TestMethod]
        public void ParsePropertiesWithoutEffectiveUserTest()
        {
            var props = @"<PropertyList xmlns=""urn:schemas-microsoft-com:xml-analysis"">
          <Catalog>Adventure Works</Catalog>
          <LocaleIdentifier>1033</LocaleIdentifier>
          <Format>Tabular</Format>
          <Content>SchemaData</Content>
          <Timeout>0</Timeout>
          <ReturnCellProperties>true</ReturnCellProperties>
          <DbpropMsmdFlattened2>true</DbpropMsmdFlattened2>
          <DbpropMsmdActivityID>8ac8460e-281b-4d4e-ba2a-23e3ace266da</DbpropMsmdActivityID>
          <DbpropMsmdCurrentActivityID>8ac8460e-281b-4d4e-ba2a-23e3ace266da</DbpropMsmdCurrentActivityID>
          <DbpropMsmdRequestID>1c71dde3-34e5-42f9-b1eb-ed951d6a3041</DbpropMsmdRequestID>
        </PropertyList>";
            QueryBeginEvent beginEvent = new QueryBeginEvent();
            beginEvent.RequestProperties = props;
            var effUser = beginEvent.ParseEffectiveUsername();
            Assert.AreEqual("", effUser);

        }

    }
}
