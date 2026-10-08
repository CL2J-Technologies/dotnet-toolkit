using System.Diagnostics;

namespace cl2j.Image
{
    /// <summary>
    /// `ImageUtils.AreImagesIdentical` with a debug trail: at `DebugLevel` 1 and above, the two
    /// originals are saved under `Identical` or `Different` in `SavePath`, so a wrong verdict can
    /// be looked at.
    ///
    /// Until 6.0.0 this class carried its own copy of the comparison, which had drifted from the
    /// one in `ImageUtils` — an `int` accumulator that overflowed on large images, intermediates
    /// never disposed. It now delegates: there is one rule, and both entry points apply it.
    /// </summary>
    public static class ImageComparer
    {
        public class ImageCompareSettings : ImageUtils.ImageCompareSettings
        {
            public int DebugLevel { get; set; }
            public string SavePath { get; set; } = null!;
            public string Name1 { get; set; } = null!;
            public string Name2 { get; set; } = null!;
        }

        public static bool AreImagesIdentical(RasterImage image1, RasterImage image2, ImageCompareSettings settings)
        {
            if (image1 == null || image2 == null)
                return false;

            var identical = ImageUtils.AreImagesIdentical(image1, image2, settings);

            if (settings.DebugLevel > 0)
            {
                var folder = Path.Combine(settings.SavePath, identical ? "Identical" : "Different");
                Directory.CreateDirectory(folder);
                ImageSerialization.SaveJpeg(Path.Combine(folder, $"{settings.Name1}.jpg"), image1);
                ImageSerialization.SaveJpeg(Path.Combine(folder, $"{settings.Name2}.jpg"), image2);
                Debug.WriteLine($"Compare {settings.Name1} & {settings.Name2} => {(identical ? "identical" : "different")}");
            }

            return identical;
        }

        public static bool Compare(RasterImage image1, RasterImage image2, out int diff) => ImageUtils.Compare(image1, image2, out diff);

        public static double Equals(RasterImage image1, RasterImage image2, int pixelDiffMax) => ImageUtils.Equals(image1, image2, pixelDiffMax);
    }
}
