
namespace TinySato
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Linq;
    using System.Text;

    public class Graphic
    {
        protected Printer printer;

        internal Graphic(Printer printer)
        {
            this.printer = printer;
        }

        public void AddBitmap(Bitmap original)
        {
            var region = new Rectangle(0, 0, original.Width, original.Height);
            using (var bmp1bpp = original.Clone(region, PixelFormat.Format1bppIndexed))
            using (var memory = new System.IO.MemoryStream())
            {
                bmp1bpp.Save(memory, ImageFormat.Bmp);
                var bmp = memory.ToArray();
                if (!(1 <= bmp.Length && bmp.Length <= 99999))
                    throw new TinySatoException(
                        string.Format("Reduce bitmap size. current:{0}, max:99999", bmp.Length));
                byte[] raw = Encoding.ASCII.GetBytes(
                    string.Format("{0}GM{1:D5},", Printer.ESC, bmp.Length))
                    .Concat(bmp).ToArray();
                this.printer.Add(raw);
            }
        }

        public void AddGraphic(Bitmap original, bool is_strict = false)
        {
            if (is_strict &&
                (original.Width % 8 != 0 || original.Height % 8 != 0))
                throw new TinySatoException("Invalid a image size. Specify the width or height of multiples of 8.");
            var region = new Rectangle(0, 0,
                original.Width - (original.Width % 8),
                original.Height - (original.Height % 8));
            using (var bmp1bpp = original.Clone(region, PixelFormat.Format1bppIndexed))
            {
                var bmp1bit = new List<byte>();
                var bmp1bit_ = new byte[bmp1bpp.Height * bmp1bpp.Width];
                const byte black = 1, white = 0;
                for (int y = 0, i = 0; y < bmp1bpp.Height; ++y)
                {
                    for (int x = 0; x < bmp1bpp.Width; ++x, ++i)
                    {
                        var color = bmp1bpp.GetPixel(x, y);
                        bmp1bit_[i] = (color.R == 0 && color.G == 0 && color.B == 0) ?
                            black : white;
                        bmp1bit.Add((color.R == 0 && color.G == 0 && color.B == 0) ?
                            black : white);
                    }
                }
                var bmp8bit = bmp1bit_.Select((bit, index) => new { Bit = bit, Index = index })
                    .GroupBy(data => data.Index / 8, data => data.Bit);
                this.printer.Add("GH" + string.Format("{0:D3}{1:D3}{2}",
                    bmp1bpp.Width / 8, bmp1bpp.Height / 8,
                    string.Join("", bmp8bit.Select(bits =>
                      ((bits.ElementAt(7))
                     + (bits.ElementAt(6) << 1)
                     + (bits.ElementAt(5) << 2)
                     + (bits.ElementAt(4) << 3)
                     + (bits.ElementAt(3) << 4)
                     + (bits.ElementAt(2) << 5)
                     + (bits.ElementAt(1) << 6)
                     + (bits.ElementAt(0) << 7)).ToString("X2")))));
            }
        }

        public void AddBox(int horizontal_line_width, int vertical_line_width, int width, int height)
        {
            if (!(1 <= horizontal_line_width && horizontal_line_width <= 99))
                throw new TinySatoException("Specify 1-99 dots.");
            if (!(1 <= vertical_line_width && vertical_line_width <= 99))
                throw new TinySatoException("Specify 1-99 dots.");
            if (!(1 <= width && width <= 9999))
                throw new TinySatoException("Specify 1-9999 dots.");
            if (!(1 <= height && height <= 9999))
                throw new TinySatoException("Specify 1-9999 dots.");
            this.printer.Add(string.Format("FW{0:D2}{1:D2}V{2:D4}H{3:D4}",
                horizontal_line_width, vertical_line_width, height, width));
        }
    }
}
