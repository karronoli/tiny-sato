
namespace TinySato
{
    using System.Linq;
    public class Barcode
    {
        protected Printer printer;
        internal Barcode(Printer printer)
        {
            this.printer = printer;
        }
        public void AddCODE128(int narrow_bar_width, int barcode_height, string print_data)
        {
            if (!(1 <= narrow_bar_width && narrow_bar_width <= 12))
                throw new TinySatoException("Specify 1-12 dot for Narrow Bar Width.");
            if (!(1 <= barcode_height && barcode_height <= 600))
                throw new TinySatoException("Specify 1-600 dot for Barcode Height.");
            this.printer.Add(string.Format("BG{0:D2}{1:D3}{2}", narrow_bar_width, barcode_height, print_data));
        }

        public void AddJAN13(int thin_bar_width, int barcode_top, string print_data)
        {
            if (!(1 <= thin_bar_width && thin_bar_width <= 12))
                throw new TinySatoException("Specify 1-12 dot for Narrow Bar Width.");
            if (!(1 <= barcode_top && barcode_top <= 600))
                throw new TinySatoException("Specify 1-600 dot for Barcode Height.");
            if (!(11 <= print_data.Length && print_data.Length <= 13))
                throw new TinySatoException("Correct barcode data length. valid range: 11-13");
            if (!print_data.All(char.IsDigit))
                throw new TinySatoException("Correct character type of barcode data.");
            this.printer.Add(string.Format("BD3{0:D2}{1:D3}{2}", thin_bar_width, barcode_top, print_data));
        }

        public void AddCodabar(int thin_bar_width, int bar_top_length, string print_data)
        {
            int codabar_ratio13 = 0;
            int sscc_disable = 0; // WIP Need?
            this.printer.Add(string.Format("B{0:D1}{1:D2}{2:D3}{3}A{4}A",
                codabar_ratio13, thin_bar_width, bar_top_length, sscc_disable,
                print_data));
        }
    }
}
