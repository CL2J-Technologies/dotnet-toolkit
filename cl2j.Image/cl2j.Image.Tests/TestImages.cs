using SkiaSharp;

namespace cl2j.Image.Tests
{
    /// <summary>Images and files built in code for the tests: nothing is read from disk, so nothing depends on the platform.</summary>
    internal static class TestImages
    {
        public static byte[] Jpeg(int width, int height) => Encode(Gradient(width, height), SKEncodedImageFormat.Jpeg);

        public static byte[] Png(int width, int height) => Encode(Gradient(width, height), SKEncodedImageFormat.Png);

        /// <summary>A 24-bit bottom-up BMP, written by hand: SkiaSharp reads BMP but does not encode it.</summary>
        public static byte[] Bmp(int width, int height)
        {
            var rowSize = (width * 3 + 3) & ~3;
            var pixels = rowSize * height;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((byte)'B'); w.Write((byte)'M'); w.Write(54 + pixels); w.Write(0); w.Write(54);
            w.Write(40); w.Write(width); w.Write(height); w.Write((short)1); w.Write((short)24);
            w.Write(0); w.Write(pixels); w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                {
                    w.Write((byte)(x % 256)); w.Write((byte)(y % 256)); w.Write((byte)((x + y) % 256));
                }
                for (var p = width * 3; p < rowSize; ++p)
                    w.Write((byte)0);
            }
            w.Flush();
            return ms.ToArray();
        }

        /// <summary>A grey image whose level is computed per pixel.</summary>
        public static RasterImage Grey(int width, int height, Func<int, int, byte> level)
        {
            var image = new RasterImage(width, height);
            image.Fill(level);
            return image;
        }

        /// <summary>
        /// Inserts an EXIF APP1 segment right after the JPEG start marker, holding a single
        /// Orientation tag, built by hand so the test depends on the EXIF format and not on a
        /// library able to write it.
        /// </summary>
        public static byte[] WithExif(byte[] jpeg, int orientation)
        {
            var tiff = new List<byte>
            {
                (byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0,     // little-endian TIFF header, IFD0 at offset 8
                1, 0,                                        // one entry
                0x12, 0x01, 3, 0, 1, 0, 0, 0,                // tag 0x0112 Orientation, type SHORT, count 1
                (byte)orientation, 0, 0, 0,                  // value
                0, 0, 0, 0,                                  // no next IFD
            };
            var payload = System.Text.Encoding.ASCII.GetBytes("Exif\0\0").Concat(tiff).ToArray();
            var length = payload.Length + 2;
            var segment = new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF) }.Concat(payload);
            return jpeg.Take(2).Concat(segment).Concat(jpeg.Skip(2)).ToArray();
        }

        /// <summary>A PNG with one extra chunk placed right after IHDR, its CRC computed so a decoder accepts it.</summary>
        public static byte[] PngWithChunk(byte[] png, string type, byte[] data)
        {
            var chunk = new List<byte>();
            chunk.AddRange(BigEndian(data.Length));
            var typeAndData = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
            chunk.AddRange(typeAndData);
            chunk.AddRange(BigEndian((int)Crc32(typeAndData)));

            const int AfterIhdr = 8 + 25;
            return png.Take(AfterIhdr).Concat(chunk).Concat(png.Skip(AfterIhdr)).ToArray();
        }

        private static byte[] BigEndian(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

        private static uint Crc32(byte[] bytes)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in bytes)
            {
                crc ^= b;
                for (var k = 0; k < 8; ++k)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
            return ~crc;
        }

        private static SKBitmap Gradient(int width, int height)
        {
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                    bitmap.SetPixel(x, y, new SKColor((byte)(x % 256), (byte)(y % 256), (byte)((x + y) % 256)));
            }
            return bitmap;
        }

        private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
        {
            using (bitmap)
            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(format, 90))
                return data.ToArray();
        }
    }
}
