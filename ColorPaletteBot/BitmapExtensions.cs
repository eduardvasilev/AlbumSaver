using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ColorPaletteBot
{
    public static class BitmapExtensions
    {
        public static Stream ToStream(this Bitmap image, ImageFormat format)
        {
            var stream = new System.IO.MemoryStream();
            image.Save(stream, format);
            stream.Position = 0;
            return stream;
        }
    }
}
