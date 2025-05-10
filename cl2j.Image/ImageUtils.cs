using System.Drawing;
using cl2j.Tooling.Exceptions;
using SixLabors.ImageSharp.Processing;

namespace cl2j.Image
{
    public static class ImageUtils
    {
        public static void OptimizeImage(ref byte[] bytes, int max = 1280, long quality = 75L)
        {
            var image = ReadImage(bytes);
            if (image != null)
            {
                var modified = ExifUtils.RotateFlipIfRequired(image);
                modified |= ExifUtils.Strip(image);

                if (image.Width > max || image.Height > max)
                {
                    image = ImageResizer.ResizeIfOversize(image, max, max);
                    modified = true;
                }

                if (modified)
                    bytes = ImageSerialization.SaveJpegToBytes(image, quality);
            }
        }

        public static Bitmap CreateThumbnailCropped(Bitmap image, int w, int h)
        {
            var currentRatio = Math.Round((decimal)image.Width / image.Height, 2);
            var targetRatio = Math.Round((decimal)w / h, 2);

            if (currentRatio == targetRatio)
                return ImageResizer.ResizeIfOversize(image, w, h);

            int newW;
            int newH;
            if (currentRatio < targetRatio)
            {
                newW = image.Width;
                newH = (int)Math.Round(image.Width / targetRatio, 0);
            }
            else
            {
                newW = (int)Math.Round(image.Height * targetRatio, 0);
                newH = image.Height;
            }

            var croppedImage = CropCenter(image, newW, newH, out _);

            if (croppedImage.Width == w && croppedImage.Height == h)
                return croppedImage;

            return ImageResizer.Resize(croppedImage, w, h);
        }

        public static Bitmap CreateThumbnail(Bitmap image, int w, int h, Color backgroundColor)
        {
            var currentRatio = Math.Round((decimal)image.Width / image.Height, 2);
            var targetRatio = Math.Round((decimal)w / h, 2);

            if (currentRatio == targetRatio)
                return ImageResizer.ResizeIfOversize(image, w, h);

            int newW;
            int newH;
            int x;
            int y;
            if (currentRatio < targetRatio)
            {
                newW = (int)Math.Round(h * currentRatio, 0);
                newH = h;
                x = (w - newW) / 2;
                y = 0;
            }
            else
            {
                newW = w;
                newH = (int)Math.Round(w / currentRatio, 0);
                x = 0;
                y = (h - newH) / 2;
            }

            var target = new Bitmap(w, h);
            using (var g = Graphics.FromImage(target))
            {
                g.Clear(backgroundColor);
                g.DrawImage(image, new RectangleF(x, y, newW, newH), new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
            }

            return target;
        }

        public static Bitmap CreateThumbnailWithRatio(Bitmap image, int w)
        {
            var ratio = Math.Round((decimal)image.Width / image.Height, 2);
            int h = (int)Math.Round(w / ratio, 0);

            var target = new Bitmap(w, h);
            using (var g = Graphics.FromImage(target))
            {
                g.DrawImage(image, new RectangleF(0, 0, w, h), new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
            }

            return target;
        }

        public static Bitmap Crop(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;

            int topmost = 0;
            for (int row = 0; row < h; ++row)
            {
                if (IsAllColorRow(bmp, row))
                    topmost = row + 1;
                else
                    break;
            }

            int bottommost = 0;
            for (int row = h - 1; row >= 0; --row)
            {
                if (IsAllColorRow(bmp, row))
                    bottommost = row;
                else
                    break;
            }

            int leftmost = 0;
            for (int col = 0; col < w; ++col)
            {
                if (IsAllColorColumn(bmp, col))
                    leftmost = col + 1;
                else
                    break;
            }

            int rightmost = 0;
            for (int col = w - 1; col >= 0; --col)
            {
                if (IsAllColorColumn(bmp, col))
                    rightmost = col;
                else
                    break;
            }

            if (rightmost == 0)
                rightmost = w; // As reached left
            if (bottommost == 0)
                bottommost = h; // As reached top.

            int croppedWidth = rightmost - leftmost;
            int croppedHeight = bottommost - topmost;

            if (croppedWidth == 0) // No border on left or right
            {
                leftmost = 0;
                croppedWidth = w;
            }

            if (croppedHeight == 0) // No border on top or bottom
            {
                topmost = 0;
                croppedHeight = h;
            }

            if (croppedWidth == bmp.Width && croppedHeight == bmp.Height)
                return bmp;

            try
            {
                var target = new Bitmap(croppedWidth, croppedHeight);
                using (Graphics g = Graphics.FromImage(target))
                {
                    g.DrawImage(bmp,
                      new RectangleF(0, 0, croppedWidth, croppedHeight),
                      new RectangleF(leftmost, topmost, croppedWidth, croppedHeight),
                      GraphicsUnit.Pixel);
                }
                return target;
            }
            catch (Exception ex)
            {
                throw new BadRequestException($"Values are topmost={topmost} btm={bottommost} left={leftmost} right={rightmost} croppedWidth={croppedWidth} croppedHeight={croppedHeight}", ex);
            }
        }

        public static Bitmap CropCenter(Bitmap bmp, int w, int h, out bool modified)
        {
            modified = false;
            if (bmp.Width < w || bmp.Height < h)
                return bmp;
            if (bmp.Width == w && bmp.Height == h)
                return bmp;

            int x = (bmp.Width - w) / 2;
            int y = (bmp.Height - h) / 2;

            var target = new Bitmap(w, h);
            using (var g = Graphics.FromImage(target))
            {
                g.DrawImage(bmp, new RectangleF(0, 0, w, h), new RectangleF(x, y, w, h), GraphicsUnit.Pixel);
            }

            modified = true;

            return target;
        }

        public class ImageCompareSettings
        {
            public int PixelDifferenceTolerance { get; set; } = 19;
            public int CompareDifferenceMax { get; set; } = 30;
            public double PourcentEqualsMin { get; set; } = 0.6;
        }

        public static bool AreImagesIdentical(Bitmap image1, Bitmap image2, ImageCompareSettings settings)
        {
            if (image1 == null || image2 == null)
                return false;

            //Crop images (remove white lines/columns) surronding
            var newImage = Crop(image2);
            var imageRatio = (double)newImage.Width / newImage.Height;

            var newImageCrawler = Crop(image1);
            var imageCrawlerRatio = (double)newImageCrawler.Width / newImageCrawler.Height;

            //Ratio is different --> Images are differents
            if (Math.Abs(imageRatio - imageCrawlerRatio) > 0.01)
                return false;

            //Resize images if required to have the same size for the comparaison
            if (newImage.Width > newImageCrawler.Width)
                newImage = ImageResizer.Resize(newImage, newImageCrawler.Width, newImageCrawler.Height);
            else
                newImageCrawler = ImageResizer.Resize(newImageCrawler, newImage.Width, newImage.Height);

            //Compare
            var res = Compare(newImage, newImageCrawler, out var diff);
            if (res)
            {
                var pourcentEquals = Equals(newImage, newImageCrawler, settings.PixelDifferenceTolerance);
                if (diff <= settings.CompareDifferenceMax && pourcentEquals >= settings.PourcentEqualsMin)
                    return true;
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

        public static byte[]? CleanImage(byte[] bytes, int max = 1280)
        {
            if (bytes != null)
            {
                try
                {
                    using var ms = new MemoryStream(bytes);
                    if (System.Drawing.Image.FromStream(ms) is Bitmap image)
                    {
                        var modifiedImage = image.CleanImage(max, out var modified);
                        if (modified)
                            return ImageSerialization.SaveJpegToBytes(modifiedImage, 75L);
                    }
                }
                catch (Exception)
                {
                    //File.WriteAllBytes(@"C:\Dev\Alertogo\Alertogo\test.jpg", bytes);

                    try
                    {
                        var white = SixLabors.ImageSharp.PixelFormats.Rgba32.ParseHex("#FFFFFF");
                        using var imageTmp = SixLabors.ImageSharp.Image.Load(bytes);
                        imageTmp.Mutate(x => x.BackgroundColor(white));

                        using var stream = new MemoryStream();
                        SixLabors.ImageSharp.ImageExtensions.SaveAsBmp(imageTmp, stream);

                        if (System.Drawing.Image.FromStream(stream) is Bitmap image)
                        {
                            var modifiedImage = image.CleanImage(max, out var modified);
                            return ImageSerialization.SaveJpegToBytes(modifiedImage, 75L);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return bytes;
        }

        public static Bitmap CleanImage(this Bitmap image, int max, out bool modified)
        {
            modified = ExifUtils.RotateFlipIfRequired(image);
            modified |= ExifUtils.Strip(image);

            if (image.Width > max || image.Height > max)
            {
                var modifiedImage = ImageResizer.ResizeIfOversize(image, max, max);
                modified = true;
                return modifiedImage;
            }

            return image;
        }

        public static Bitmap GenerateDiffImage(Bitmap image1, Bitmap image2)
        {
            if (image1.Width != image2.Width || image1.Height != image2.Height)
                throw new BadRequestException("Images sizes must match");

            var bitmap = new Bitmap(image1.Width, image1.Height);
            for (int x = 0; x < image1.Width; ++x)
            {
                for (int y = 0; y < image1.Height; ++y)
                {
                    var c1 = image1.GetPixel(x, y);
                    var c2 = image2.GetPixel(x, y);
                    bitmap.SetPixel(x, y, c1.Diff(c2));
                }
            }
            return bitmap;
        }

        public static bool IsAllColorRow(Bitmap image, int n)
        {
            for (int i = 0; i < image.Width; ++i)
            {
                var p = image.GetPixel(i, n);
                if (!p.CloseToWhite())
                    return false;
            }
            return true;
        }

        public static bool IsAllColorColumn(Bitmap image, int n)
        {
            for (int i = 0; i < image.Height; ++i)
            {
                var p = image.GetPixel(n, i);
                if (!p.CloseToWhite())
                    return false;
            }
            return true;
        }

        public static bool CloseToWhite(this Color c, byte threshold = 230)
        {
            var g = c.ToGrayscale();
            return g >= threshold;
        }

        public static byte ToGrayscale(this Color c)
        {
            return (byte)(0.3 * c.R + 0.59 * c.G + 0.11 * c.B);
        }

        public static int DiffGrayscale(this Color c1, Color c2)
        {
            var g1 = c1.ToGrayscale();
            var g2 = c2.ToGrayscale();
            return Math.Abs(g1 - g2);
        }

        public static Color Diff(this Color c1, Color c2)
        {
            var r = Math.Abs(c1.R - c2.R);
            var g = Math.Abs(c1.G - c2.G);
            var b = Math.Abs(c1.B - c2.B);
            return Color.FromArgb(r, g, b);
        }

        public static Bitmap? ReadImage(byte[] bytes)
        {
            if (bytes != null)
            {
                try
                {
                    using var ms = new MemoryStream(bytes);
                    if (System.Drawing.Image.FromStream(ms) is Bitmap image)
                        return image;
                }
                catch (Exception)
                {
                    try
                    {
                        var white = SixLabors.ImageSharp.PixelFormats.Rgba32.ParseHex("#FFFFFF");
                        using var imageTmp = SixLabors.ImageSharp.Image.Load(bytes);
                        imageTmp.Mutate(x => x.BackgroundColor(white));

                        using var stream = new MemoryStream();
                        SixLabors.ImageSharp.ImageExtensions.SaveAsBmp(imageTmp, stream);

                        if (System.Drawing.Image.FromStream(stream) is Bitmap image)
                            return image;
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            return null;
        }
    }
}