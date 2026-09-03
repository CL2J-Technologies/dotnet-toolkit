using ImageRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace cl2j.Image
{
    public class ImageResizer
    {
        //Porte sur ImageSharp le 3 septembre 2026. System.Drawing leve
        //PlatformNotSupportedException sur tout ce qui n est pas Windows depuis .NET 6, et le site
        //Appartogo tourne sous Linux : son portail n a jamais produit une seule vignette, sans une
        //ligne d erreur. Voir l entree s19 du journal du depot cl2j.
        //
        //Les images rendues appartiennent a l appelant, qui doit les liberer. C etait deja la
        //convention avec Bitmap ; le portage ne l a pas changee.
        public static ImageRgba32 Resize(ImageRgba32 image, int newWidth, int newHeight)
        {
            if (image.Width == newWidth && image.Height == newHeight)
                return image;

            //Bicubique, comme l ancien InterpolationMode.HighQualityBicubic.
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

            //Une image tres allongee pouvait rendre une dimension nulle, sur quoi Bitmap levait.
            //On borne a 1 : ImageSharp leve aussi, et un panorama ne doit pas faire tomber un
            //cycle d agregation.
            return Resize(image, Math.Max(1, newW), Math.Max(1, newH));
        }
    }
}
