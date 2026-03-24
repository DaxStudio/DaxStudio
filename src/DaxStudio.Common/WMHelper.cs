using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static DaxStudio.Common.NativeMethods;

namespace DaxStudio.Common
{
    public static class WMHelper
    {
        /// <summary>
        /// Serializes a string array to a byte array using length-prefixed UTF8 encoding.
        /// Compatible with both .NET Framework and .NET 8+.
        /// </summary>
        public static byte[] SerializeStringArray(string[] args)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(args.Length);
                foreach (var arg in args)
                    writer.Write(arg ?? string.Empty);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes a string array from a byte array using length-prefixed UTF8 encoding.
        /// </summary>
        public static string[] DeserializeStringArray(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                int count = reader.ReadInt32();
                var args = new string[count];
                for (int i = 0; i < count; i++)
                    args[i] = reader.ReadString();
                return args;
            }
        }

        /// <summary>
        /// Deserializes a string array from a stream using length-prefixed UTF8 encoding.
        /// </summary>
        public static string[] DeserializeStringArray(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                int count = reader.ReadInt32();
                var args = new string[count];
                for (int i = 0; i < count; i++)
                    args[i] = reader.ReadString();
                return args;
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
