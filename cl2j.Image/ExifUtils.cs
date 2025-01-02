using System.Drawing;

namespace cl2j.Image
{
    public static class ExifUtils
    {
        public static bool RotateFlipIfRequired(Bitmap image)
        {
            if (Array.IndexOf(image.PropertyIdList, 274) > -1)
            {
                //1 = Horizontal(normal). No rotation required.
                //2 = Mirror horizontal
                //3 = Rotate 180
                //4 = Mirror vertical
                //5 = Mirror horizontal and rotate 270 CW
                //6 = Rotate 90 CW
                //7 = Mirror horizontal and rotate 90 CW
                //8 = Rotate 270 CW

                var pi = image.GetPropertyItem(274);
                if (pi != null && pi.Value != null && pi.Value.Length > 0)
                {
                    var orientation = (int)pi.Value[0];
                    switch (orientation)
                    {
                        case 1:
                            break;

                        case 2:
                            image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                            break;

                        case 3:
                            image.RotateFlip(RotateFlipType.Rotate180FlipNone);
                            break;

                        case 4:
                            image.RotateFlip(RotateFlipType.Rotate180FlipX);
                            break;

                        case 5:
                            image.RotateFlip(RotateFlipType.Rotate90FlipX);
                            break;

                        case 6:
                            image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                            break;

                        case 7:
                            image.RotateFlip(RotateFlipType.Rotate270FlipX);
                            break;

                        case 8:
                            image.RotateFlip(RotateFlipType.Rotate270FlipNone);
                            break;
                    }

                    if (orientation > 1)
                    {
                        // This EXIF data is now invalid and should be removed.
                        image.RemovePropertyItem(274);
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool Strip(Bitmap image)
        {
            var modified = image.PropertyItems.Length > 0;

            while (image.PropertyItems.Length > 0)
            {
                var pi = image.PropertyItems[0];
                image.RemovePropertyItem(pi.Id);
            }

            return modified;
        }
    }
}