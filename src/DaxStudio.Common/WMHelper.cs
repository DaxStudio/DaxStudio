using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using static DaxStudio.Common.NativeMethods;

namespace DaxStudio.Common
{
    public static class WMHelper
    {
        public static void SendCopyDataMessage(IntPtr hwnd, string[] args)
        {
            COPYDATASTRUCT cds;
            // Serialize our raw string data into a json stream
            using (MemoryStream stream = new MemoryStream())
            using (StreamWriter writer = new StreamWriter(stream))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JsonSerializer ser = new JsonSerializer();
                ser.Serialize(jsonWriter, args);
                jsonWriter.Flush();

                int dataSize = (int)stream.Length;

                // Create byte array and transfer the stream data
                byte[] bytes = new byte[dataSize];
                stream.Seek(0, SeekOrigin.Begin);
                stream.Read(bytes, 0, dataSize);
                stream.Close();
         
                // Allocate a memory address for our byte array
                IntPtr ptrData = Marshal.AllocCoTaskMem(dataSize);

                // Copy the byte data into this memory address
                Marshal.Copy(bytes, 0, ptrData, dataSize);

                
                cds.dwData = (IntPtr)100;
                cds.lpData = ptrData;
                cds.cbData = dataSize;
            }
            Common.NativeMethods.SendMessage(hwnd, Common.NativeMethods.WM_COPYDATA, 0, ref cds);
        }

        public static string[] ReceiveCopyDataMessage(IntPtr lParam)
        {

            // msg.LParam contains a pointer to the COPYDATASTRUCT struct
            NativeMethods.COPYDATASTRUCT dataStruct = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);

            // Create a byte array to hold the data
            byte[] bytes = new byte[dataStruct.cbData];

            // Make a copy of the original data referenced by 
            // the COPYDATASTRUCT struct
            Marshal.Copy(dataStruct.lpData, bytes, 0,dataStruct.cbData);

            // Deserialize the data back into a string
            using (MemoryStream stream = new MemoryStream(bytes))
            using (StreamReader reader = new StreamReader(stream))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                JsonSerializer ser = new JsonSerializer();
                // This is the message sent from the other application
                string[] rawmessage = ser.Deserialize<string[]>(jsonReader);

                // do something with our message

                return rawmessage;
            }

        }

    }
}
