using ImageRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using cl2j.Tooling.Exceptions;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ISImage = SixLabors.ImageSharp.Image;

namespace cl2j.Image
{
    /// <summary>
    /// Utilitaires d image, sur ImageSharp.
    ///
    /// **Porte depuis System.Drawing le 3 septembre 2026.** Le motif n est pas la modernisation :
    /// `System.Drawing.Common` leve `PlatformNotSupportedException` sur tout ce qui n est pas
    /// Windows depuis .NET 6. Le site Appartogo tourne sous Linux, et son portail n avait donc
    /// **jamais** produit une seule vignette depuis son ouverture en juin 2025 — 5 400 images,
    /// zero vignette, sans une ligne d erreur, parce que les deux points d entree avalaient
    /// l exception pour rendre `null` ou les octets d origine. Voir l entree s19 du journal du
    /// depot cl2j.
    ///
    /// La meme panne attendait le crawler : elle se serait declenchee le jour ou l agregation
    /// quitte la VM Windows pour une Function ou un conteneur Linux.
    ///
    /// **Convention de propriete, inchangee :** les images rendues appartiennent a l appelant, qui
    /// doit les liberer. Certaines methodes rendent l instance recue quand il n y a rien a faire —
    /// `Resize` a taille egale, `Crop` sans bordure, `CropCenter` sur une image deja plus petite.
    /// Ne pas liberer un resultat sans savoir s il s agit de l original.
    /// </summary>
    public static class ImageUtils
    {
        public class OptimizeReasult
        {
            public int Width { get; set; }
            public int Height { get; set; }

            public bool Modified { get; set; }
        }

        public static OptimizeReasult? OptimizeImage(ref byte[] bytes, int max = 1280, long quality = 75L)
        {
            using var image = ReadImage(bytes);
            if (image != null)
            {
                var modified = ExifUtils.RotateFlipIfRequired(image);
                modified |= ExifUtils.Strip(image);

                if (image.Width > max || image.Height > max)
                {
                    using var redimensionnee = ImageResizer.ResizeIfOversize(image, max, max);
                    bytes = ImageSerialization.SaveJpegToBytes(redimensionnee, quality);

                    return new OptimizeReasult
                    {
                        Modified = true,
                        Width = redimensionnee.Width,
                        Height = redimensionnee.Height
                    };
                }

                if (modified)
                    bytes = ImageSerialization.SaveJpegToBytes(image, quality);

                return new OptimizeReasult
                {
                    Modified = modified,
                    Width = image.Width,
                    Height = image.Height
                };
            }

            return null;
        }

        public static ImageRgba32 CreateThumbnailCropped(byte[] bytes, int w, int h)
        {
            using var image = ReadImage(bytes) ?? throw new ValidationException("Invalid image");
            return CreateThumbnailCropped(image, w, h);
        }

        public static ImageRgba32 CreateThumbnailCropped(ImageRgba32 image, int w, int h)
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

            var croppedImage = CropCenter(image, newW, newH, out var recadree);

            if (croppedImage.Width == w && croppedImage.Height == h)
                return recadree ? croppedImage : croppedImage.Clone();

            var vignette = ImageResizer.Resize(croppedImage, w, h);
            if (recadree && !ReferenceEquals(vignette, croppedImage))
                croppedImage.Dispose();
            return vignette;
        }

        public static ImageRgba32 CreateThumbnail(ImageRgba32 image, int w, int h, Rgba32 backgroundColor)
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

            using var redimensionnee = ImageResizer.Resize(image, Math.Max(1, newW), Math.Max(1, newH));

            var target = new ImageRgba32(w, h);
            target.Mutate(g =>
            {
                g.BackgroundColor(backgroundColor);
                g.DrawImage(redimensionnee, new SixLabors.ImageSharp.Point(x, y), 1f);
            });

            return target;
        }

        public static ImageRgba32 CreateThumbnailWithRatio(ImageRgba32 image, int w)
        {
            var ratio = Math.Round((decimal)image.Width / image.Height, 2);
            int h = (int)Math.Round(w / ratio, 0);

            return ImageResizer.Resize(image, w, Math.Max(1, h));
        }

        public static ImageRgba32 Crop(ImageRgba32 bmp)
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

            //Une image entierement blanche donne des bornes croisees : l ancienne version levait
            //alors une BadRequestException depuis Graphics.DrawImage. On garde le meme signal,
            //mais leve avant plutot que d attendre la bibliotheque.
            if (croppedWidth <= 0 || croppedHeight <= 0 || leftmost + croppedWidth > w || topmost + croppedHeight > h)
                throw new BadRequestException($"Values are topmost={topmost} btm={bottommost} left={leftmost} right={rightmost} croppedWidth={croppedWidth} croppedHeight={croppedHeight}");

            return bmp.Clone(x => x.Crop(new SixLabors.ImageSharp.Rectangle(leftmost, topmost, croppedWidth, croppedHeight)));
        }

        public static ImageRgba32 CropCenter(ImageRgba32 bmp, int w, int h, out bool modified)
        {
            modified = false;
            if (bmp.Width < w || bmp.Height < h)
                return bmp;
            if (bmp.Width == w && bmp.Height == h)
                return bmp;

            int x = (bmp.Width - w) / 2;
            int y = (bmp.Height - h) / 2;

            modified = true;

            return bmp.Clone(c => c.Crop(new SixLabors.ImageSharp.Rectangle(x, y, w, h)));
        }

        public class ImageCompareSettings
        {
            public int PixelDifferenceTolerance { get; set; } = 19;
            public int CompareDifferenceMax { get; set; } = 30;
            public double PourcentEqualsMin { get; set; } = 0.6;
        }

        public static bool AreImagesIdentical(ImageRgba32 image1, ImageRgba32 image2, ImageCompareSettings settings)
        {
            if (image1 == null || image2 == null)
                return false;

            ImageRgba32? newImage = null;
            ImageRgba32? newImageCrawler = null;
            try
            {
                //Crop images (remove white lines/columns) surronding
                newImage = Crop(image2);
                var imageRatio = (double)newImage.Width / newImage.Height;

                newImageCrawler = Crop(image1);
                var imageCrawlerRatio = (double)newImageCrawler.Width / newImageCrawler.Height;

                //Ratio is different --> Images are differents
                if (Math.Abs(imageRatio - imageCrawlerRatio) > 0.01)
                    return false;

                //Resize images if required to have the same size for the comparaison
                if (newImage.Width > newImageCrawler.Width)
                    newImage = Remplacer(newImage, image2, ImageResizer.Resize(newImage, newImageCrawler.Width, newImageCrawler.Height));
                else
                    newImageCrawler = Remplacer(newImageCrawler, image1, ImageResizer.Resize(newImageCrawler, newImage.Width, newImage.Height));

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
            finally
            {
                //Les intermediaires sont a nous, les originaux non. Crop et Resize rendent parfois
                //l instance recue : c est ce que verifie la comparaison de reference. Sans ce soin,
                //l agregation liberait les images de son appelant — et sur des dizaines de milliers
                //de comparaisons, ne rien liberer du tout coutait la memoire.
                Liberer(newImage, image1, image2);
                Liberer(newImageCrawler, image1, image2);
            }
        }

        private static ImageRgba32 Remplacer(ImageRgba32 ancienne, ImageRgba32 original, ImageRgba32 nouvelle)
        {
            if (!ReferenceEquals(ancienne, original) && !ReferenceEquals(ancienne, nouvelle))
                ancienne.Dispose();
            return nouvelle;
        }

        private static void Liberer(ImageRgba32? image, ImageRgba32 original1, ImageRgba32 original2)
        {
            if (image is not null && !ReferenceEquals(image, original1) && !ReferenceEquals(image, original2))
                image.Dispose();
        }

        public static bool Compare(ImageRgba32 image1, ImageRgba32 image2, out int diff)
        {
            diff = 0;

            if (image1.Width != image2.Width || image1.Height != image2.Height)
                return false;

            long total = 0;
            for (int y = 0; y < image1.Height; ++y)
            {
                for (int x = 0; x < image1.Width; ++x)
                    total += image1[x, y].DiffGrayscale(image2[x, y]);
            }

            diff = (int)(total / (image1.Width * image1.Height));

            return true;
        }

        public static double Equals(ImageRgba32 image1, ImageRgba32 image2, int pixelDiffMax)
        {
            if (image1.Width != image2.Width || image1.Height != image2.Height)
                return 0;

            int nbPixelEquals = 0;
            for (int y = 0; y < image1.Height; ++y)
            {
                for (int x = 0; x < image1.Width; ++x)
                {
                    if (image1[x, y].DiffGrayscale(image2[x, y]) <= pixelDiffMax)
                        ++nbPixelEquals;
                }
            }

            return (double)nbPixelEquals / (image1.Width * image1.Height);
        }

        public static byte[]? CleanImage(byte[] bytes, int max = 1280)
        {
            if (bytes == null)
                return bytes;

            //Plus de repli, et c est le coeur du correctif. L ancienne version enchainait deux
            //tentatives qui se terminaient toutes les deux par System.Drawing, puis rendait les
            //octets d origine sans rien dire : sous Linux, toute image ressortait telle quelle,
            //non redimensionnee — 2,4 Mo mesures sur une photo du portail.
            //
            //On laisse desormais l exception remonter. Une image illisible est une erreur que
            //l appelant doit voir, pas un silence a stocker.
            using var image = ISImage.Load<Rgba32>(bytes);

            var modified = ExifUtils.RotateFlipIfRequired(image);
            modified |= ExifUtils.Strip(image);

            if (image.Width > max || image.Height > max)
            {
                using var redimensionnee = ImageResizer.ResizeIfOversize(image, max, max);
                return ImageSerialization.SaveJpegToBytes(redimensionnee, 75L);
            }

            return modified ? ImageSerialization.SaveJpegToBytes(image, 75L) : bytes;
        }

        public static ImageRgba32 CleanImage(this ImageRgba32 image, int max, out bool modified)
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

        public static ImageRgba32 GenerateDiffImage(ImageRgba32 image1, ImageRgba32 image2)
        {
            if (image1.Width != image2.Width || image1.Height != image2.Height)
                throw new BadRequestException("Images sizes must match");

            var result = new ImageRgba32(image1.Width, image1.Height);
            for (int y = 0; y < image1.Height; ++y)
            {
                for (int x = 0; x < image1.Width; ++x)
                    result[x, y] = image1[x, y].Diff(image2[x, y]);
            }
            return result;
        }

        public static bool IsAllColorRow(ImageRgba32 image, int n)
        {
            for (int i = 0; i < image.Width; ++i)
            {
                if (!image[i, n].CloseToWhite())
                    return false;
            }
            return true;
        }

        public static bool IsAllColorColumn(ImageRgba32 image, int n)
        {
            for (int i = 0; i < image.Height; ++i)
            {
                if (!image[n, i].CloseToWhite())
                    return false;
            }
            return true;
        }

        public static bool CloseToWhite(this Rgba32 c, byte threshold = 230)
        {
            var g = c.ToGrayscale();
            return g >= threshold;
        }

        public static byte ToGrayscale(this Rgba32 c)
        {
            return (byte)(0.3 * c.R + 0.59 * c.G + 0.11 * c.B);
        }

        public static int DiffGrayscale(this Rgba32 c1, Rgba32 c2)
        {
            var g1 = c1.ToGrayscale();
            var g2 = c2.ToGrayscale();
            return Math.Abs(g1 - g2);
        }

        public static Rgba32 Diff(this Rgba32 c1, Rgba32 c2)
        {
            var r = (byte)Math.Abs(c1.R - c2.R);
            var g = (byte)Math.Abs(c1.G - c2.G);
            var b = (byte)Math.Abs(c1.B - c2.B);
            return new Rgba32(r, g, b);
        }

        /// <summary>
        /// Rend l image, ou `null` si les octets ne sont pas une image lisible.
        ///
        /// ⚠️ **Le `null` est silencieux, et c est ce qui a coute quinze mois.** Sous Linux, cette
        /// methode rendait `null` pour *toutes* les images, et `cl2j.Medias.MediaService` ignorait
        /// ce `null` : aucune vignette n a jamais ete produite, sans une ligne de journal. Un
        /// appelant qui ne peut rien faire d un `null` doit lever ou journaliser, jamais continuer.
        /// </summary>
        public static ImageRgba32? ReadImage(byte[] bytes)
        {
            if (bytes == null)
                return null;

            try
            {
                return ISImage.Load<Rgba32>(bytes);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
