using System.Diagnostics;
using System.Drawing;

namespace cl2j.Image
{
    public static class ImageComparer
    {
        public class ImageCompareSettings
        {
            public int PixelDifferenceTolerance { get; set; } = 19;
            public int CompareDifferenceMax { get; set; } = 30;
            public double PourcentEqualsMin { get; set; } = 0.6;
            public int DebugLevel { get; set; }
            public string SavePath { get; set; } = null!;
            public string Name1 { get; set; } = null!;
            public string Name2 { get; set; } = null!;
        }

        public static bool AreImagesIdentical(Bitmap image1, Bitmap image2, ImageCompareSettings settings)
        {
            if (image1 == null || image2 == null)
                return false;

            if (settings.DebugLevel > 1)
            {
                ImageSerialization.SaveJpeg(Path.Combine(settings.SavePath, $"{settings.Name1}_orig.jpg"), image1);
                ImageSerialization.SaveJpeg(Path.Combine(settings.SavePath, $"{settings.Name2}_orig.jpg"), image2);
            }

            //Crop images (remove white lines/columns) surronding
            var newImage = ImageUtils.Crop(image2);
            var imageRatio = (double)newImage.Width / newImage.Height;
            var newImageCrawler = ImageUtils.Crop(image1);
            var imageCrawlerRatio = (double)newImageCrawler.Width / newImageCrawler.Height;
            if (settings.DebugLevel > 1)
            {
                ImageSerialization.SaveJpeg(Path.Combine(settings.SavePath, $"{settings.Name2}_crop.jpg"), newImage);
                ImageSerialization.SaveJpeg(Path.Combine(settings.SavePath, $"{settings.Name1}_crop.jpg"), newImageCrawler);
            }

            //Ratio is different --> Images are differents
            if (Math.Abs(imageRatio - imageCrawlerRatio) > 0.01)
            {
                if (settings.DebugLevel > 1)
                {
                    ImageSerialization.SaveJpeg(Path.Combine(settings.SavePath, $"DifferentRatios\\{settings.Name2}.jpg"), image2);
                    ImageSerialization.SaveJpeg(Path.Combine(settings.SavePath, $"DifferentRatios\\{settings.Name1}.jpg"), image1);
                }
                return false;
            }

            //Resize images if required to have the same size for the comparaison
            if (newImage.Width > newImageCrawler.Width)
            {
                newImage = ImageResizer.Resize(newImage, newImageCrawler.Width, newImageCrawler.Height);
                if (settings.DebugLevel > 1)
                    ImageSerialization.SaveJpeg(Path.Combine(settings.SavePath, $"{settings.Name2}_resize.jpg"), newImage);
            }
            else
            {
                newImageCrawler = ImageResizer.Resize(newImageCrawler, newImage.Width, newImage.Height);
                if (settings.DebugLevel > 1)
                    ImageSerialization.SaveJpeg(Path.Combine(settings.SavePath, $"{settings.Name1}_resize.jpg"), newImageCrawler);
            }

            //Compare
            if (Compare(newImage, newImageCrawler, out var diff))
            {
                var pourcentEquals = Equals(newImage, newImageCrawler, settings.PixelDifferenceTolerance);
                if (settings.DebugLevel > 1)
                    Debug.WriteLine($"Diff {settings.Name1} & {settings.Name2} => {diff} : equality={pourcentEquals * 100:0.00}%");

                if (diff <= settings.CompareDifferenceMax && pourcentEquals >= settings.PourcentEqualsMin)
                {
                    if (settings.DebugLevel > 0)
                    {
                        ImageSerialization.SaveJpeg(Path.Combine(settings.SavePath, $"Identical\\{settings.Name2}.jpg"), image2);
                        ImageSerialization.SaveJpeg(Path.Combine(settings.SavePath, $"Identical\\{settings.Name1}.jpg"), image1);
                    }
                    return true;
                }

                if (settings.DebugLevel > 0)
                {
                    ImageSerialization.SaveJpeg(Path.Combine(settings.SavePath, $"DifferentColors\\{settings.Name2}.jpg"), image2);
                    ImageSerialization.SaveJpeg(Path.Combine(settings.SavePath, $"DifferentColors\\{settings.Name1}.jpg"), image1);
                }
            }

            return false;
        }

        public static bool Compare(Bitmap image1, Bitmap image2, out int diff)
        {
            diff = 0;

            if (image1.Width != image2.Width || image1.Height != image2.Height)
                return false;

            for (int x = 0; x < image1.Width; ++x)
            {
                for (int y = 0; y < image1.Height; ++y)
                {
                    var c1 = image1.GetPixel(x, y);
                    var c2 = image2.GetPixel(x, y);
                    diff += c1.DiffGrayscale(c2);
                }
            }

            diff /= image1.Width * image1.Height;

            return true;
        }

        public static double Equals(Bitmap image1, Bitmap image2, int pixelDiffMax)
        {
            if (image1.Width != image2.Width || image1.Height != image2.Height)
                return 0;

            int nbPixelEquals = 0;
            for (int x = 0; x < image1.Width; ++x)
            {
                for (int y = 0; y < image1.Height; ++y)
                {
                    var c1 = image1.GetPixel(x, y);
                    var c2 = image2.GetPixel(x, y);
                    var diff = c1.DiffGrayscale(c2);
                    if (diff <= pixelDiffMax)
                        ++nbPixelEquals;
                }
            }

            return (double)nbPixelEquals / (image1.Width * image1.Height);
        }
    }
}