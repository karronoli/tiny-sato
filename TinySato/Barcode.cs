
namespace TinySato
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Text.RegularExpressions;

    public class Barcode
    {
        public enum Ratio
        {
            NARROW1_WIDE2,
            NARROW1_WIDE3,
            NARROW2_WIDE5
        }

        protected Printer printer;

        internal Barcode(Printer printer)
        {
            this.printer = printer;
        }

        public void AddCODE128(int narrow_bar_width, int barcode_height, string print_data)
        {
            if (!(1 <= narrow_bar_width && narrow_bar_width <= 12))
                throw new TinySatoArgumentException("Specify 1-12 dot for Narrow Bar Width.");
            if (!(1 <= barcode_height && barcode_height <= 600))
                throw new TinySatoArgumentException("Specify 1-600 dot for Barcode Height.");
            this.printer.Add(string.Format("BG{0:D2}{1:D3}{2}", narrow_bar_width, barcode_height, print_data));
        }

        public void AddCODE128(int narrow_bar_width, int barcode_height, string print_data, Action<Size> set_position_by_size)
        {
            if (!(1 <= narrow_bar_width && narrow_bar_width <= 12))
                throw new TinySatoArgumentException("Specify 1-12 dot for Narrow Bar Width.");
            if (!(1 <= barcode_height && barcode_height <= 600))
                throw new TinySatoArgumentException("Specify 1-600 dot for Barcode Height.");

            var m = (new Regex(@"(\d{6,})$")).Match(print_data);
            var index = m.Success ?
                m.Index + m.Captures[0].Value.Length % 2 :
                print_data.Length;
            var front = print_data.Substring(0, index);
            var back = print_data.Substring(index);
            // 	refer to JIS X 0504:2003
            var width = 11 * narrow_bar_width // start
                    + 11 * front.Length * narrow_bar_width // front
                    + 11 * narrow_bar_width // check
                    + 13 * narrow_bar_width; // stop
            if (back.Length > 0)
            {
                var width_set_c =
                    11 * narrow_bar_width // shift
                    + 11 * back.Length / 2 * narrow_bar_width; // back
                set_position_by_size(new Size(width + width_set_c, barcode_height));
                this.printer.Add(string.Format("BG{0:D2}{1:D3}{2}",
                    narrow_bar_width, barcode_height,
                    ">H" + front + ">C" + back));
            }
            else
            {
                set_position_by_size(new Size(width, barcode_height));
                this.printer.Add(string.Format("BG{0:D2}{1:D3}{2}",
                    narrow_bar_width, barcode_height,
                    ">H" + front));
            }
        }

        public void AddJAN13(int thin_bar_width, int barcode_top, string print_data)
        {
            if (!(1 <= thin_bar_width && thin_bar_width <= 12))
                throw new TinySatoArgumentException("Specify 1-12 dot for Narrow Bar Width.");
            if (!(1 <= barcode_top && barcode_top <= 600))
                throw new TinySatoArgumentException("Specify 1-600 dot for Barcode Height.");
            if (!(11 <= print_data.Length && print_data.Length <= 13))
                throw new TinySatoArgumentException("Correct barcode data length. valid range: 11-13");
            if (!print_data.All(char.IsDigit))
                throw new TinySatoArgumentException("Correct character type of barcode data.");
            this.printer.Add(string.Format("BD3{0:D2}{1:D3}{2}", thin_bar_width, barcode_top, print_data));
        }

        protected static Dictionary<Ratio, string> RatioOperands { get; }
        = new Dictionary<Ratio, string>()
        {
            { Ratio.NARROW1_WIDE2, "D" },
            { Ratio.NARROW1_WIDE3, "B" },
            { Ratio.NARROW2_WIDE5, "BD" }
        };
        protected static string CodabarStartStopCharacters { get; } = "ABCD";
        protected static string CodabarDataCharacters { get; } = "0123456789-$:/.+";
        public static string CodabarSymbols { get; } = CodabarDataCharacters + CodabarStartStopCharacters;

        public void AddCodabar(int thin_bar_width, int bar_top_length, string print_data,
            char start_stop_char = 'A')
        {
            AddCodabar(Ratio.NARROW1_WIDE3, thin_bar_width, bar_top_length, print_data,
                start_stop_char, start_stop_char);
        }

        public void AddCodabar(Ratio r, int thin_bar_width, int bar_top_length, string print_data,
            char start_stop_char = 'A')
        {
            AddCodabar(r, thin_bar_width, bar_top_length, print_data,
                start_stop_char, start_stop_char);
        }

        /// <summary>
        /// Specify a 1:2 or 1:3 or 2:5 ratio Codabar barcode with a narrow bar and wide bar.
        /// </summary>
        /// <param name="r">Ratio.</param>
        /// <param name="thin_bar_width">Thin Bar Width.</param>
        /// <param name="bar_top_length">Bar Top Length.</param>
        /// <param name="print_data">Print Data.</param>
        /// <param name="start_char">Start Character.</param>
        /// <param name="stop_char">Stop Character.</param>
        public void AddCodabar(Ratio r, int thin_bar_width, int bar_top_length, string print_data,
            char start_char, char stop_char)
        {
            if (!(1 <= thin_bar_width && thin_bar_width <= 12))
                throw new TinySatoArgumentException("Specify 1-12 dot for Narrow Bar Width.");
            if (!(1 <= bar_top_length && bar_top_length <= 600))
                throw new TinySatoArgumentException("Specify 1-600 dot for Barcode Height.");
            if (!print_data.All(pd => CodabarDataCharacters.Contains(pd)))
                throw new TinySatoArgumentException("Check character of barcode data.");
            if (!CodabarStartStopCharacters.Contains(start_char))
                throw new TinySatoArgumentException("Check start character.");
            if (!CodabarStartStopCharacters.Contains(stop_char))
                throw new TinySatoArgumentException("Check stop character.");

            const int barcode_type = 0; // 0: Codabar
            string print_data_with_control =
                string.Format("{0}{1}{2}", start_char, print_data, stop_char);
            this.printer.Add(string.Format("{0}{1:D1}{2:D2}{3:D3}{4}",
                RatioOperands[r], barcode_type,
                thin_bar_width, bar_top_length, print_data_with_control));
        }
    }
}
