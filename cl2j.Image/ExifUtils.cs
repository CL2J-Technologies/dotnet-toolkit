using ImageRgba32 = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace cl2j.Image
{
    public static class ExifUtils
    {
        //Ported to ImageSharp on September 3rd 2026, see ImageResizer for why.
        //
        //The old version unrolled by hand the eight values of tag 274 into RotateFlipType.
        //ImageSharp does exactly that work in AutoOrient, including the four mirrored cases, and
        //strips the tag behind it. We keep the return value — "was the image modified" — which
        //callers use to decide whether to re-encode.
        public static bool RotateFlipIfRequired(ImageRgba32 image)
        {
            var orientation = LireOrientation(image);

            //1 = Horizontal (normal), nothing to do. Absent, nothing to do either.
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
