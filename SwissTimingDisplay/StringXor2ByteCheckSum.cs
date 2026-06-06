using System.Text;

namespace SwissTimingDisplay
{
    public static class StringXor2ByteCheckSum
    {
        /// <summary>
        /// Calculates XOR checksum of all bytes in the string and returns as 2-character hex string
        /// </summary>
        public static string AsString(string msg)
        {
            byte cs = 0;
            foreach (char c in msg)
            {
                cs ^= (byte)c;
            }
            return cs.ToString("X2");
        }

        public static byte AsByte(string msg)
        {
            byte cs = 0;
            foreach (char c in msg)
            {
                cs ^= (byte)c;
            }
            return cs;
        }

        /// <summary>
        /// Calculates XOR checksum and returns as ASCII byte array
        /// </summary>
        public static byte[] CalculateAsBytes(string msg)
        {
            return Encoding.ASCII.GetBytes(AsString(msg));
        }
    }
}
