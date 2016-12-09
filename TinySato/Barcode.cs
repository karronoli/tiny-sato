
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

        protected static string CodabarStartStopCharacters { get; } = "ABCD";
        protected static string CodabarDataCharacters { get; } = "0123456789-$:/.+";
        public static string CodabarSymbols { get; } = CodabarDataCharacters + CodabarStartStopCharacters;

        /// <summary>
        /// Specifies a 1:3 ratio Codabar barcode with a narrow bar and wide bar.
        /// </summary>
        /// <param name="thin_bar_width">Thin Bar Width</param>
        /// <param name="bar_top_length">Bar Top Length</param>
        /// <param name="print_data">Print Data</param>
        /// <param name="start_stop_char">Start / Stop Character</param>
        public void AddCodabar13(int thin_bar_width, int bar_top_length, string print_data,
            char start_stop_char = 'A')
        {
            if (!(1 <= thin_bar_width && thin_bar_width <= 12))
                throw new TinySatoException("Specify 1-12 dot for Narrow Bar Width.");
            if (!(1 <= bar_top_length && bar_top_length <= 600))
                throw new TinySatoException("Specify 1-600 dot for Barcode Height.");
            if (!print_data.All(pd => CodabarDataCharacters.Contains(pd)))
                throw new TinySatoException("Check character of barcode data.");
            if (!CodabarStartStopCharacters.Contains(start_stop_char))
                throw new TinySatoException("Check start/stop character.");

            const int barcode_type = 0; // 0: Codabar
            const int sscc_parameter = 0; // It does not affect anything.
            string print_data_with_control =
                string.Format("{0}{1}{0}", start_stop_char, print_data);
            this.printer.Add(string.Format("B{0:D1}{1:D2}{2:D3}{3}{4}",
                barcode_type, thin_bar_width, bar_top_length, sscc_parameter,
                print_data_with_control));
        }
    }
}
