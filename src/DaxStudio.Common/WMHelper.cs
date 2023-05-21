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
            // Serialize our raw string data into a binary stream
            BinaryFormatter b = new BinaryFormatter();
            MemoryStream stream = new MemoryStream();
            b.Serialize(stream, args);
            stream.Flush();
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

            COPYDATASTRUCT cds;
            cds.dwData = (IntPtr)100;
            cds.lpData = ptrData;
            cds.cbData = dataSize;

            Common.NativeMethods.SendMessage(hwnd, Common.NativeMethods.WM_COPYDATA, 0, ref cds);
        }
    }
}
