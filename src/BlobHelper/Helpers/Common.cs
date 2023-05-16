using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace BlobHelper
{
    internal class Common
    {
        public static byte[] Base64ToBytes(string data)
        {
            try
            {
                return System.Convert.FromBase64String(data);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string Base64ToUTF8(string data)
        {
            try
            {
                if (String.IsNullOrEmpty(data)) return null;
                byte[] bytes = System.Convert.FromBase64String(data);
                return System.Text.UTF8Encoding.UTF8.GetString(bytes);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string BytesToBase64(byte[] data)
        {
            if (data == null) return null;
            if (data.Length < 1) return null;
            return System.Convert.ToBase64String(data);
        }

        public static string UTF8ToBase64(string data)
        {
            try
            {
                if (String.IsNullOrEmpty(data)) return null;
                byte[] bytes = System.Text.UTF8Encoding.UTF8.GetBytes(data);
                return System.Convert.ToBase64String(bytes);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static byte[] StreamToBytes(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }

        public static double TotalMsFrom(DateTime start)
        {
            try
            {
                DateTime end = DateTime.Now;
                return TotalMsBetween(start, end);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static double TotalMsBetween(DateTime start, DateTime end)
        {
            try
            {
                start = start.ToUniversalTime();
                end = end.ToUniversalTime();
                TimeSpan total = end - start;
                return total.TotalMilliseconds;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static bool IsLaterThanNow(DateTime? dt)
        {
            try
            {
                DateTime curr = Convert.ToDateTime(dt);
                return Common.IsLaterThanNow(curr);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsLaterThanNow(DateTime dt)
        {
            if (DateTime.Compare(dt, DateTime.Now) > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
