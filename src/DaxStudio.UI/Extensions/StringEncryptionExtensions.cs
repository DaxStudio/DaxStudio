using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Security;
using System.Runtime.InteropServices;

namespace DaxStudio.UI.Extensions
{
    public static class StringEncryptionExtensions
    {
        private static byte[] key = { 125, 217, 19, 11, 24, 26, 85, 45, 114, 184, 27, 162, 37, 112, 222, 209, 241, 24, 175, 144, 173, 53, 196, 29, 24, 26, 17, 218, 131, 236, 53, 209 };
        private static byte[] entropy = { 125, 217, 19, 11, 24, 26, 85, 45 };
        //private static byte[] vector = { 146, 64, 191, 111, 23, 3, 113, 119, 231, 121, 221, 112, 79, 32, 114, 156 };
        private static UTF8Encoding encoder = new UTF8Encoding();

        public static string ConvertToUnsecureString(this SecureString securePassword)
        {
            if (securePassword == null)
            {
                return string.Empty;
            }

            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }

        public static string Encrypt(this string unencrypted)
        {
            if (unencrypted.Trim() == string.Empty) return string.Empty;
            return Convert.ToBase64String(ProtectedData.Protect(encoder.GetBytes(unencrypted), entropy, DataProtectionScope.CurrentUser));
            
            //            return Convert.ToBase64String( Encrypt(encoder.GetBytes(unencrypted)));
        }

        public static string Decrypt(this string encrypted)
        {
            if (encrypted == string.Empty) return string.Empty;
            return encoder.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encrypted),entropy, DataProtectionScope.CurrentUser));
            //return encoder.GetString(Decrypt(Convert.FromBase64String(encrypted)));
        }

        public static string GetInsecureString(this SecureString secure)
        {
            IntPtr insecureBSTR = default(IntPtr);
            try
            {
                insecureBSTR = Marshal.SecureStringToBSTR(secure);
                return Marshal.PtrToStringBSTR(insecureBSTR);
            }
            catch
            {
                return "";
            }
        }


        /*
        internal static byte[] Encrypt(byte[] buffer)
        {
            using (RijndaelManaged rm = new RijndaelManaged())
            using ( ICryptoTransform encryptor = rm.CreateEncryptor(key,vector)){
                return Transform(buffer, encryptor);
            }
        }

        internal static byte[] Decrypt(byte[] buffer)
        {
            using (RijndaelManaged rm = new RijndaelManaged())
            using (ICryptoTransform decryptor = rm.CreateDecryptor(key,vector)) {
                return Transform(buffer, decryptor);
            }
        }

        internal static byte[] Transform(byte[] buffer, ICryptoTransform transform)
        {
            
            using (MemoryStream stream = new MemoryStream()) {
                using (CryptoStream cs = new CryptoStream(stream, transform, CryptoStreamMode.Write))
                {
                    cs.Write(buffer, 0, buffer.Length);
                }
                return stream.ToArray();
            }
        }
         */ 
    }
}
