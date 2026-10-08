namespace cl2j.Image
{
    /// <summary>
    /// Tells whether an image file carries metadata — EXIF, XMP, IPTC or an ICC profile — by
    /// walking its container, without decoding anything.
    ///
    /// **Why it exists.** `CleanImage` re-encodes a photo that carries metadata, to strip it: an
    /// EXIF block holds the camera, the date and sometimes the GPS position of the flat. The
    /// previous engine (ImageSharp) exposed the profiles after decoding; SkiaSharp does not, so the
    /// question is answered from the bytes. The four kinds counted are the four ImageSharp counted,
    /// so the decision to re-encode is unchanged.
    /// </summary>
    internal static class MetadataSniffer
    {
        public static bool HasMetadata(byte[] bytes)
        {
            if (bytes is null || bytes.Length < 12)
                return false;
            if (bytes[0] == 0xFF && bytes[1] == 0xD8)
                return Jpeg(bytes);
            if (bytes[0] == 0x89 && bytes[1] == (byte)'P' && bytes[2] == (byte)'N' && bytes[3] == (byte)'G')
                return Png(bytes);
            if (Ascii(bytes, 0, "RIFF") && Ascii(bytes, 8, "WEBP"))
                return WebP(bytes);
            return false;
        }

        // JPEG: the APPn segments before the first scan. APP1 holds EXIF or XMP, APP2 the ICC
        // profile, APP13 the Photoshop block that carries IPTC.
        private static bool Jpeg(byte[] b)
        {
            var i = 2;
            while (i + 4 <= b.Length && b[i] == 0xFF)
            {
                var marker = b[i + 1];
                if (marker == 0xFF) { ++i; continue; }
                if (marker == 0xDA || marker == 0xD9)
                    return false;
                var length = (b[i + 2] << 8) | b[i + 3];
                var payload = i + 4;
                if ((marker == 0xE1 && (Ascii(b, payload, "Exif\0") || Ascii(b, payload, "http://ns.adobe.com/xap/")))
                    || (marker == 0xE2 && Ascii(b, payload, "ICC_PROFILE"))
                    || (marker == 0xED && Ascii(b, payload, "Photoshop 3.0")))
                    return true;
                i += 2 + length;
            }
            return false;
        }

        // PNG: eXIf, iCCP, and the iTXt chunk whose keyword is the XMP one.
        private static bool Png(byte[] b)
        {
            var i = 8;
            while (i + 8 <= b.Length)
            {
                var length = (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
                if (length < 0)
                    return false;
                var type = System.Text.Encoding.ASCII.GetString(b, i + 4, 4);
                if (type is "eXIf" or "iCCP")
                    return true;
                if (type == "iTXt" && Ascii(b, i + 8, "XML:com.adobe.xmp"))
                    return true;
                if (type == "IEND")
                    return false;
                i += 12 + length;
            }
            return false;
        }

        // WebP: the extended header VP8X declares its ICC, EXIF and XMP chunks in a flags byte.
        private static bool WebP(byte[] b)
        {
            if (!Ascii(b, 12, "VP8X") || b.Length < 21)
                return false;
            const byte Icc = 0x20, Exif = 0x08, Xmp = 0x04;
            return (b[20] & (Icc | Exif | Xmp)) != 0;
        }

        private static bool Ascii(byte[] b, int offset, string text)
        {
            if (offset < 0 || offset + text.Length > b.Length)
                return false;
            for (var k = 0; k < text.Length; ++k)
            {
                if (b[offset + k] != (byte)text[k])
                    return false;
            }
            return true;
        }
    }
}
