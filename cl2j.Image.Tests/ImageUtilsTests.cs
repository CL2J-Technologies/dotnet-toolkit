using cl2j.Image;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using ImageRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace cl2j.Image.Tests
{
    /// <summary>
    /// Ces tests existent a cause d une panne precise : `cl2j.Image` reposait sur `System.Drawing`,
    /// qui leve `PlatformNotSupportedException` hors Windows depuis .NET 6. Le site Appartogo
    /// tourne sous Linux, et son portail n a donc produit **aucune vignette** entre juin 2025 et
    /// septembre 2026 — 5 400 images, zero vignette, sans une ligne d erreur, parce que les points
    /// d entree avalaient l exception pour rendre `null` ou les octets d origine.
    ///
    /// La CI lance `dotnet test` sur `ubuntu-latest`. Ces tests s executent donc exactement sur le
    /// systeme ou la panne se produisait : ils la rattraperaient au premier coup.
    /// </summary>
    public class ImageUtilsTests
    {
        [Fact]
        public void CleanImage_redimensionne_une_image_trop_grande()
        {
            var octets = Jpeg(4000, 2252);

            var nettoyee = ImageUtils.CleanImage(octets, 1280);

            Assert.NotNull(nettoyee);
            using var image = ImageUtils.ReadImage(nettoyee!);
            Assert.NotNull(image);
            Assert.Equal(1280, image!.Width);
            Assert.True(image.Height <= 1280);
            Assert.True(nettoyee!.Length < octets.Length, "l image nettoyee doit peser moins que l originale");
        }

        [Fact]
        public void CleanImage_laisse_les_dimensions_d_une_image_deja_petite()
        {
            var octets = Jpeg(640, 480);

            var nettoyee = ImageUtils.CleanImage(octets, 1280);

            using var image = ImageUtils.ReadImage(nettoyee!);
            Assert.Equal(640, image!.Width);
            Assert.Equal(480, image.Height);
        }

        [Fact]
        public void CleanImage_leve_sur_des_octets_illisibles()
        {
            //Le silence etait le defaut : l ancienne version rendait les octets d origine, si bien
            //qu une image illisible se stockait telle quelle sans que personne le sache.
            Assert.ThrowsAny<Exception>(() => ImageUtils.CleanImage([1, 2, 3, 4, 5], 1280));
        }

        [Fact]
        public void CreateThumbnailCropped_rend_une_vignette_450x337_pour_du_640x480()
        {
            //Dimensions relevees le 3 septembre 2026 sur une vignette reelle du CDN, produite par
            //l ancienne implementation System.Drawing depuis une source 640x480. Le port doit
            //rendre exactement la meme geometrie.
            using var source = new ImageRgba32(640, 480);

            using var vignette = ImageUtils.CreateThumbnailCropped(source, 450, 338);

            Assert.Equal(450, vignette.Width);
            Assert.Equal(337, vignette.Height);
        }

        [Fact]
        public void CreateThumbnailCropped_produit_un_jpeg_relisible()
        {
            using var source = ImageUtils.ReadImage(Jpeg(4000, 2252))!;

            using var vignette = ImageUtils.CreateThumbnailCropped(source, 450, 338);
            var octets = ImageSerialization.SaveJpegToBytes(vignette, 75L);

            Assert.NotEmpty(octets);
            Assert.Equal("JPEG", SixLabors.ImageSharp.Image.Identify(octets).Metadata.DecodedImageFormat?.Name);

            using var relue = ImageUtils.ReadImage(octets);
            Assert.NotNull(relue);
            Assert.Equal(450, relue!.Width);
            Assert.Equal(338, relue.Height);
        }

        [Fact]
        public void ReadImage_rend_null_sur_des_octets_illisibles()
        {
            Assert.Null(ImageUtils.ReadImage([1, 2, 3, 4, 5]));
        }

        [Fact]
        public void IsImage_reconnait_un_jpeg()
        {
            Assert.True(ImageUtils.IsImage(Jpeg(64, 48)));
        }

        [Theory]
        [InlineData("<!DOCTYPE html>\r\n<html lang=\"fr\"><head><title>LogisQuebec</title></head></html>")]
        [InlineData("")]
        [InlineData("not an image at all")]
        public void IsImage_refuse_ce_qui_n_est_pas_une_image(string contenu)
        {
            //Le cas qui compte est le premier : une source qui redirige ses photos supprimees vers
            //sa page d accueil rend du HTML avec un code 200, et HttpClient suit la redirection
            //tout seul. Sans cette garde, la page est stockee sous un nom en .jpg.
            Assert.False(ImageUtils.IsImage(System.Text.Encoding.UTF8.GetBytes(contenu)));
        }

        [Fact]
        public void IsImage_refuse_des_octets_absents()
        {
            Assert.False(ImageUtils.IsImage(null!));
        }

        [Fact]
        public void Strip_retire_le_profil_exif()
        {
            using var image = new ImageRgba32(10, 10);
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Copyright, "cl2j");

            var modifie = ExifUtils.Strip(image);

            Assert.True(modifie);
            Assert.Null(image.Metadata.ExifProfile);
        }

        [Fact]
        public void RotateFlipIfRequired_applique_l_orientation_puis_la_retire()
        {
            //Orientation 6 = rotation de 90 degres : une image large doit ressortir haute.
            using var image = new ImageRgba32(100, 50);
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);

            var modifie = ExifUtils.RotateFlipIfRequired(image);

            Assert.True(modifie);
            Assert.Equal(50, image.Width);
            Assert.Equal(100, image.Height);
        }

        [Fact]
        public void RotateFlipIfRequired_ne_touche_pas_une_image_deja_droite()
        {
            using var image = new ImageRgba32(100, 50);

            Assert.False(ExifUtils.RotateFlipIfRequired(image));
            Assert.Equal(100, image.Width);
        }

        //Un degrade plutot qu une image unie : un JPEG uni se compresse a presque rien, et le test
        //de reduction de taille ne prouverait alors pas grand-chose.
        private static byte[] Jpeg(int largeur, int hauteur)
        {
            using var image = new ImageRgba32(largeur, hauteur);
            for (var y = 0; y < hauteur; ++y)
            {
                for (var x = 0; x < largeur; ++x)
                    image[x, y] = new Rgba32((byte)(x % 256), (byte)(y % 256), (byte)((x + y) % 256));
            }

            using var ms = new MemoryStream();
            image.Save(ms, new JpegEncoder { Quality = 90 });
            return ms.ToArray();
        }
    }
}
