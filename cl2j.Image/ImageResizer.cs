using System.Drawing;
using System.Drawing.Drawing2D;

namespace cl2j.Image
{
    public class ImageResizer
    {
        public static Bitmap Resize(Bitmap image, int newWidth, int newHeight)
        {
            if (image.Width == newWidth && image.Height == newHeight)
                return image;

            var res = new Bitmap(newWidth, newHeight);

            using (var graphic = Graphics.FromImage(res))
            {
                graphic.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphic.SmoothingMode = SmoothingMode.HighQuality;
                graphic.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphic.CompositingQuality = CompositingQuality.HighQuality;
                graphic.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            return res;
        }

        public static Bitmap ResizeIfOversize(Bitmap image, int maxW, int maxH)
        {
            int newW;
            int newH;
            if (image.Width > image.Height)
            {
                newW = maxW;
                newH = image.Height * maxW / image.Width;
            }
            else
            {
                newW = image.Width * maxH / image.Height;
                newH = maxH;
            }

            return Resize(image, newW, newH);
        }
    }
}