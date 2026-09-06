using ImageRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace cl2j.Image
{
    public class ImageResizer
    {
        //Ported to ImageSharp on September 3rd 2026. System.Drawing throws
        //PlatformNotSupportedException on anything but Windows since .NET 6, and the Appartogo
        //site runs on Linux: its portal never produced a single thumbnail, without one line of
        //error. See entry s19 of the cl2j repository journal.
        //
        //The images returned belong to the caller, who must dispose them. That was already the
        //convention with Bitmap; the port did not change it.
        public static ImageRgba32 Resize(ImageRgba32 image, int newWidth, int newHeight)
        {
            if (image.Width == newWidth && image.Height == newHeight)
                return image;

            //Bicubic, like the old InterpolationMode.HighQualityBicubic.
            return image.Clone(x => x.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(newWidth, newHeight),
                Sampler = KnownResamplers.Bicubic,
                Mode = ResizeMode.Stretch
            }));
        }

        public static ImageRgba32 ResizeIfOversize(ImageRgba32 image, int maxW, int maxH)
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

            //A very elongated image could yield a zero dimension, on which Bitmap threw. We clamp
            //to 1: ImageSharp throws as well, and a panorama must not bring down an aggregation
            //cycle.
            return Resize(image, Math.Max(1, newW), Math.Max(1, newH));
        }
    }
}
