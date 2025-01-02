using System.Drawing.Imaging;
using cl2j.Tooling.Exceptions;

namespace cl2j.Image
{
    public class ImageSerialization
    {
        public static void SaveJpeg(string path, System.Drawing.Image img, long quality = 75L)
        {
            var jpegCodec = GetEncoderInfo("image/jpeg");
            var encoderParams = CreateEncoder(quality);

            img.Save(path, jpegCodec, encoderParams);
        }

        public static byte[] SaveJpegToBytes(System.Drawing.Image image, long quality = 75L)
        {
            var jpegCodec = GetEncoderInfo("image/jpeg");
            var encoderParams = CreateEncoder(quality);

            using var ms = new MemoryStream();
            image.Save(ms, jpegCodec, encoderParams);
            return ms.ToArray();
        }

        private static EncoderParameters CreateEncoder(long quality)
        {
            var qualityParam = new EncoderParameter(Encoder.Quality, quality);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = qualityParam;
            return encoderParams;
        }

        public static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            var jpegCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(t => t.MimeType == mimeType) ?? throw new NotFoundException($"{mimeType} codec doesn't exists.");
            return jpegCodec;
        }
    }
}