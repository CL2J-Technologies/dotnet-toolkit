using ImageRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace cl2j.Image
{
    public static class ExifUtils
    {
        //Porte sur ImageSharp le 3 septembre 2026, voir ImageResizer pour le pourquoi.
        //
        //L ancienne version deroulait a la main les huit valeurs du tag 274 en RotateFlipType.
        //ImageSharp fait exactement ce travail dans AutoOrient, y compris les quatre cas mirroir,
        //et retire l etiquette derriere lui. On garde la valeur de retour — « l image a-t-elle ete
        //modifiee » — que les appelants utilisent pour decider s il faut reencoder.
        public static bool RotateFlipIfRequired(ImageRgba32 image)
        {
            var orientation = LireOrientation(image);

            //1 = Horizontal (normal), rien a faire. Absente, rien a faire non plus.
            if (orientation is null or 1)
                return false;

            image.Mutate(x => x.AutoOrient());
            return true;
        }

        public static bool Strip(ImageRgba32 image)
        {
            var metadata = image.Metadata;
            var modified = metadata.ExifProfile is not null
                || metadata.IptcProfile is not null
                || metadata.XmpProfile is not null
                || metadata.IccProfile is not null;

            metadata.ExifProfile = null;
            metadata.IptcProfile = null;
            metadata.XmpProfile = null;
            metadata.IccProfile = null;

            return modified;
        }

        private static ushort? LireOrientation(ImageRgba32 image)
        {
            var profile = image.Metadata.ExifProfile;
            if (profile is null)
                return null;

            return profile.TryGetValue(ExifTag.Orientation, out var value) && value.Value is ushort orientation
                ? orientation
                : null;
        }
    }
}
