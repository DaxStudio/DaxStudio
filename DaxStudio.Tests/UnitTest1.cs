using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DaxFormatterTests
    {
        

        [TestMethod]
        public void TestValidDax()
        {
            var uri = "http://www.daxformatter.com/api/daxformatter/DaxFormat";
            var uri2 = "http://daxtest02.azurewebsites.net/api/daxformatter/daxformat";
            var data = "{Dax:'evaluate FILTER(tatatata, blah[x]=1) ', ListSeparator:',', DecimalSeparator:'.'}";
            var enc = System.Text.Encoding.UTF8;
            var data1 = enc.GetBytes(data);

            var wr = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri2);
            wr.ContentType = "application/json";
            wr.Method = "POST";
            wr.Accept = "application/json, text/javascript, */*; q=0.01";
            wr.Headers.Add("Accept-Encoding", "gzip,deflate");
            wr.Headers.Add("Accept-Language", "en-US,en;q=0.8");
            wr.ContentType = "application/json; charset=UTF-8";
            var strm = wr.GetRequestStream();
            strm.Write(data1, 0, data1.Length);

            var resp = wr.GetResponse();
            var outStrm = new System.IO.Compression.GZipStream(resp.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
            var reader = new System.IO.StreamReader(outStrm);
            var output = reader.ReadToEnd();

            Assert.AreEqual("\"EVALUATE\\r\\nFILTER ( tatatata, blah[x] = 1 )\\r\\n\\r\\n\"", output);
        }

        [TestMethod]
        public void TestInvalidDax()
        {
            var uri = "http://www.daxformatter.com/api/daxformatter/DaxFormat";
            var uri2 = "http://daxtest02.azurewebsites.net/api/daxformatter/daxformat";
            var data = "{Dax:'evaluate values(tatatata', ListSeparator:',', DecimalSeparator:'.'}";
            var enc = System.Text.Encoding.UTF8;
            var data1 = enc.GetBytes(data);

            var wr = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri2);
            wr.ContentType = "application/json";
            wr.Method = "POST";
            wr.Accept = "application/json, text/javascript, */*; q=0.01";
            wr.Headers.Add("Accept-Encoding", "gzip,deflate");
            wr.Headers.Add("Accept-Language", "en-US,en;q=0.8");
            wr.ContentType = "application/json; charset=UTF-8";
            var strm = wr.GetRequestStream();
            strm.Write(data1, 0, data1.Length);

            var resp = wr.GetResponse();
            var outStrm = new System.IO.Compression.GZipStream(resp.GetResponseStream(), System.IO.Compression.CompressionMode.Decompress);
            var reader = new System.IO.StreamReader(outStrm);
            var output = reader.ReadToEnd();

            Assert.AreNotEqual("\"\"", output);
        }

    }
}
