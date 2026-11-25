`cl2j.Image` is a small image toolkit for .NET: read an uploaded photo, straighten it from its EXIF orientation, strip its metadata, resize it, make thumbnails, encode it as JPEG or WebP, and tell whether two images show the same picture. It is built on [SkiaSharp](https://github.com/mono/SkiaSharp) and works on Windows, macOS and Linux.

Use it when a web application receives photos and must store them in a form every browser renders: bounded in size, upright, without the camera and GPS metadata, and re-encoded when the format is not a web format.

# Getting started

```powershell
Install-Package cl2j.Image
```

Target framework: `net10.0`.

The typical case is cleaning an uploaded file before storing it:

```cs
using cl2j.Image;

byte[] upload = ...; // bytes received from the client

// Upright, no metadata, longest side at most 1280 px, JPEG quality 75.
// Throws ValidationException ("Invalid image: ...") if the bytes are not a readable image.
byte[]? cleaned = ImageUtils.CleanImage(upload, max: 1280);
```

`CleanImage` returns the **same array** it received when the image is already a web format (JPEG, PNG, WebP or GIF), upright, within `max`, and carries no metadata. Otherwise it returns new JPEG bytes (quality 75). It returns `null` only for a `null` input.

To clean and upload in one call to any [cl2j.FileStorage](../cl2j.FileStorage/README.md) provider:

```cs
await MediaHelpers.UploadMediaAsync(fileStorageProvider, "photos/flat.jpg", upload, max: 1280);
```

# Platform notes

`cl2j.Image` references `SkiaSharp` and `SkiaSharp.NativeAssets.Linux.NoDependencies`, so **a Linux consumer needs nothing more**: the native `libSkiaSharp` comes with the package, and the `NoDependencies` flavour does not require fontconfig on the host. Windows and macOS native assets come with SkiaSharp itself.

Earlier versions relied on `System.Drawing.Common` (which throws `PlatformNotSupportedException` off Windows since .NET 6) and then on ImageSharp. Version 6.0.0 moved to SkiaSharp, and the public API exposes no third-party image type: `RasterImage` is the library's own.

# Reading images

`RasterImage` is the image type of the library: 8-bit RGBA pixels, `Width`, `Height`, `GetPixel(x, y)` and `Clone()`. It is `IDisposable`. `new RasterImage(width, height)` creates a transparent blank image.

```cs
// null on failure; 'failure' says why ("empty", "HEIC, which this decoder does not read", ...)
using RasterImage? image = ImageUtils.ReadImage(bytes, out string? failure);
```

| Method | What it does |
|---|---|
| `ReadImage(bytes)` / `ReadImage(bytes, out failure)` | Decodes the image. Returns `null` when it cannot. The EXIF orientation is **not** applied: `Width` and `Height` are the stored ones. |
| `ReadImage(bytes, ReadSettings, out failure)` | Same, with options. `ReadSettings.ConvertToSrgb` (default `false`, added in 6.4.0) converts the pixels to sRGB through the file's ICC profile, so a Display P3 photo from a phone is not dulled once re-encoded without a profile. |
| `Identify(bytes)` | Reads only the header and returns an `ImageHeader(Width, Height, FrameCount, Format)` (format in upper case: `JPEG`, `PNG`, `WEBP`, `GIF`...), or `null`. No pixel decoding. |
| `IsImage(bytes)` | `true` if `Identify` recognises the bytes. Useful to reject an HTML page that a redirect served instead of a photo. |
| `IsWebFormat(bytes)` | `true` for JPEG, PNG, WebP and GIF. |
| `NameUnsupportedFormat(bytes)` | Names a format this library does not decode (`"TIFF"`, `"HEIC"`, `"AVIF"`, `"video MP4"`, `"video QuickTime"`) so a refusal can be explained. Returns `null` otherwise. |

```cs
var settings = new ImageUtils.ReadSettings { ConvertToSrgb = true };
using var image = ImageUtils.ReadImage(bytes, settings, out var failure);
```

# Cleaning and optimizing

| Method | What it does |
|---|---|
| `ImageUtils.CleanImage(byte[] bytes, int max = 1280)` | Straightens, strips metadata, resizes to `max`, re-encodes as needed. See above. |
| `ImageUtils.CleanImage(this RasterImage image, int max, out bool modified)` | The same on a decoded image. Returns the input instance when it only needed straightening or stripping, or a new resized image. |
| `ImageUtils.OptimizeImage(ref byte[] bytes, int max = 1280, long quality = 75)` | Like `CleanImage`, but replaces `bytes` in place, takes a quality, and returns an `OptimizeResult` (`Width`, `Height`, `Modified`), or `null` if the image cannot be read. |
| `ExifUtils.RotateFlipIfRequired(image)` | Applies the pending EXIF orientation exactly (no resampling) and marks the image upright. Returns `true` if pixels moved. |
| `ExifUtils.Strip(image)` | Forgets the metadata (EXIF, XMP, IPTC, ICC) so the next encoding writes none. Returns `true` if there was any. |

The encoders of this library never write metadata, so "stripping" means re-encoding. Metadata presence is detected by walking the JPEG, PNG or WebP container, without decoding.

# Resizing and thumbnails

```cs
using var source = ImageUtils.ReadImage(bytes)!;
ExifUtils.RotateFlipIfRequired(source);

// 200 x 200, centre-cropped to fill the box
using var square = ImageUtils.CreateThumbnailCropped(source, 200, 200);

// Fits in 200 x 200, centred on a white background (letterboxed)
using var boxed = ImageUtils.CreateThumbnail(source, 200, 200, RgbColor.White);

// 320 px wide, height from the aspect ratio
using var banner = ImageUtils.CreateThumbnailWithRatio(source, 320);
```

| Method | What it does |
|---|---|
| `ImageResizer.Resize(image, w, h)` | Scales to exactly `w` x `h` (ratio not preserved). Large reductions are halved first, then a Catmull-Rom pass, to avoid aliasing. |
| `ImageResizer.ResizeIfOversize(image, maxW, maxH)` | Scales down, aspect ratio kept, until both dimensions fit the limits; an image already within them is returned as the same instance. |
| `ImageUtils.CreateThumbnailCropped(bytes or image, w, h)` | Centre-crops to the target ratio, then resizes. The `byte[]` overload throws `ValidationException("Invalid image")` for unreadable bytes. |
| `ImageUtils.CreateThumbnail(image, w, h, RgbColor)` | Fits inside `w` x `h` on a solid background. |
| `ImageUtils.CreateThumbnailWithRatio(image, w)` | Given width, height derived from the image ratio. |
| `ImageUtils.Crop(image)` | Removes near-white border rows and columns. |
| `ImageUtils.CropCenter(image, w, h, out modified)` | Centre crop; returns the input when it is already that size or smaller. |

# Encoding

```cs
byte[] jpeg = ImageSerialization.SaveJpegToBytes(image, quality: 80);
byte[] webp = ImageSerialization.SaveWebpToBytes(image, quality: 80); // since 6.3.0
ImageSerialization.SaveJpeg("out.jpg", image);
Stream stream = ImageSerialization.SaveJpegToStream(image);
```

- Quality is clamped to 1..100, default 75.
- Neither encoder writes metadata.
- JPEG ignores alpha; WebP keeps it.
- Skia switches to **lossless** WebP at quality 100, which is usually heavier than the JPEG. Stay below 100 for photos.
- AVIF encoding is not supported.

`MediaHelpers.GetContentTypeFromFileName(name)` returns `image/jpeg` for `.jpg` and `.jpeg`, `image/webp` for `.webp`, and `null` for anything else.

# Comparing images

`ImageUtils.AreImagesIdentical(image1, image2, settings)` crops white borders, scales the larger image down to the smaller one, and compares grey levels. It answers "do these two files show the same photo, whatever their size?".

```cs
// Defaults: PixelDifferenceTolerance 19, CompareDifferenceMax 30, PercentEqualsMin 0.6
var settings = new ImageUtils.ImageCompareSettings();
bool same = ImageUtils.AreImagesIdentical(a, b, settings);
```

Lower-level helpers: `ImageUtils.Compare(a, b, out diff)` (mean grey difference, `false` if the sizes differ), `ImageUtils.Equals(a, b, pixelDiffMax)` (share of matching pixels, 0 if the sizes differ) and `ImageUtils.GenerateDiffImage(a, b)`.

`ImageComparer.AreImagesIdentical` gives the same verdict with a debug trail: with `DebugLevel` of 1 or more, both images are saved as JPEG under `Identical` or `Different` in `SavePath`, named `Name1` and `Name2`.

# Ownership and gotchas

- **Dispose what you get.** `RasterImage` holds native memory. But several methods **return the instance they received** when there is nothing to do: `ImageResizer.Resize` at the same size, `Crop` with no border, `CropCenter` on an image already small enough, `CleanImage(RasterImage, ...)` with nothing to resize. Compare with `ReferenceEquals` before disposing a result (`MediaHelpers` does this).
- **Not decoded:** HEIC, AVIF and TIFF. Use `NameUnsupportedFormat` to explain the refusal and convert client-side. `CleanImage` throws `cl2j.Tooling.Exceptions.ValidationException` for them.
- **`ReadImage` is lenient with truncated files**: it decodes what is there, as browsers do.
- **`ConvertToSrgb` is off by default** because it changes the pixels of wide-gamut photos, and therefore thumbnails and `AreImagesIdentical` verdicts compared with images read before.
- `MediaHelpers.UploadMediaAsync(provider, name, byte[], max)` stores nothing for `null` bytes and propagates the exception for unreadable ones. The overload taking a `RasterImage` always writes JPEG bytes, whatever the file name says.
- `RasterImage.GetPixel` returns the colour without alpha.
