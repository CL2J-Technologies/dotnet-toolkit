using System.Text;

namespace cl2j.Tooling
{
    public static class StreamUtils
    {
        public static Stream? ToStream(this string text)
        {
            if (text == null)
                return null;

            var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public static byte[]? ToBytes(this Stream input)
        {
            if (input == null)
                return null;

            using var ms = new MemoryStream();
            CopyTo(input, ms);
            return ms.ToArray();
        }

        public static void CopyTo(Stream src, Stream dest)
        {
            if (src.CanSeek && src.Position > 0 && src.Length > 0)
                src.Seek(0, SeekOrigin.Begin);

            byte[] bytes = new byte[4096];
            int cnt;
            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
                dest.Write(bytes, 0, cnt);
        }
    }
}