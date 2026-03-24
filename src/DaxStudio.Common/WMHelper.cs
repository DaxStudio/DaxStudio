using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static DaxStudio.Common.NativeMethods;

namespace DaxStudio.Common
{
    public static class WMHelper
    {
        /// <summary>
        /// Serializes a string array to a UTF8 byte array using JSON.
        /// </summary>
        public static byte[] SerializeStringArray(string[] args)
        {
            var json = JsonConvert.SerializeObject(args);
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// Deserializes a string array from a UTF8 byte array.
        /// </summary>
        public static string[] DeserializeStringArray(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<string[]>(json);
        }

        /// <summary>
        /// Deserializes a string array from a stream.
        /// </summary>
        public static string[] DeserializeStringArray(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
            {
                var json = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<string[]>(json);
            }
        }

        public static void SendCopyDataMessage(IntPtr hwnd, string[] args)
        {
            byte[] bytes = SerializeStringArray(args);
            int dataSize = bytes.Length;

            // Allocate a memory address for our byte array
            IntPtr ptrData = Marshal.AllocCoTaskMem(dataSize);

            // Copy the byte data into this memory address
            Marshal.Copy(bytes, 0, ptrData, dataSize);

            COPYDATASTRUCT cds;
            cds.dwData = (IntPtr)100;
            cds.lpData = ptrData;
            cds.cbData = dataSize;

            Common.NativeMethods.SendMessage(hwnd, Common.NativeMethods.WM_COPYDATA, 0, ref cds);
        }
    }
}
