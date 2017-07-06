
namespace TinySato
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Linq;
    using System.Runtime.InteropServices;
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

            var bmp = original.LockBits(region, ImageLockMode.ReadOnly, PixelFormat.Format1bppIndexed);
            var bmp1bit = new byte[original.Height * Math.Abs(bmp.Stride)];
            Marshal.Copy(bmp.Scan0, bmp1bit, 0, bmp1bit.Length);
            original.UnlockBits(bmp);

            this.printer.Add("GH" + string.Format("{0:D3}{1:D3}{2}",
                region.Width / 8, region.Height / 8,
                string.Join("",
                    bmp1bit.Select(bits => (byte)~bits)
                    .Select(bits => bits.ToString("X2")))));
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
