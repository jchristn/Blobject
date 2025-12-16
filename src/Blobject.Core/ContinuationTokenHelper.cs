namespace Blobject.Core
{
    using System;
    using System.Text;

    internal static class ContinuationTokenHelper
    {
        internal static bool ParseContinuationToken(string continuationToken, out int start, out int count)
        {
            start = -1;
            count = -1;
            if (String.IsNullOrEmpty(continuationToken)) return false;
            byte[] encoded = Convert.FromBase64String(continuationToken);
            string encodedStr = Encoding.UTF8.GetString(encoded);
            string[] parts = encodedStr.Split(' ');
            if (parts.Length != 2) return false;

            if (!Int32.TryParse(parts[0], out start)) return false;
            if (!Int32.TryParse(parts[1], out count)) return false;
            return true;
        }

        internal static string BuildContinuationToken(long start, int count)
        {
            string ret = start.ToString() + " " + count.ToString();
            byte[] retBytes = Encoding.UTF8.GetBytes(ret);
            return Convert.ToBase64String(retBytes);
        }
    }
}