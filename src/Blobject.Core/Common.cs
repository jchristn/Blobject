﻿namespace Blobject.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Commonly-used methods.
    /// </summary>
    public class Common
    {
        /// <summary>
        /// Read a stream fully.
        /// </summary>
        /// <param name="str">Input stream.</param>
        /// <returns>Byte array.</returns>
        public static byte[] ReadStreamFully(Stream str)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));
            if (!str.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while ((read = str.Read(buffer, 0, buffer.Length)) > 0)
                    ms.Write(buffer, 0, read);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Convert byte array to hex string.
        /// </summary>
        /// <param name="bytes">Bytes.</param>
        /// <returns>Hex string.</returns>
        public static string BytesToHexString(byte[] bytes)
        {
            // NOT supported in netstandard2.1!
            // return Convert.ToHexString(bytes);  

            return BitConverter.ToString(bytes).Replace("-", "");
        }

        /// <summary>
        /// Convert hex string to byte array.
        /// </summary>
        /// <param name="hex">Hex string.</param>
        /// <returns>Bytes.</returns>
        public static byte[] BytesFromHexString(string hex)
        {
            // NOT supported in netstandard2.1!
            // return Convert.FromHexString(hex);

            int chars = hex.Length;
            byte[] bytes = new byte[chars / 2];
            for (int i = 0; i < chars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }
}
