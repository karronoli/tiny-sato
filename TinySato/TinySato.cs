using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.IO;

namespace TinySato
{
    // https://msdn.microsoft.com/library/cc398781.aspx
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string pDocName;
        [MarshalAs(UnmanagedType.LPStr)]
        public string pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)]
        public string pDataType;
    }

    public enum SensorType
    {
        Reflection = 0,
        Transparent = 1,
        Ignore = 2
    }

    public enum DensitySpec
    {
        A, B, C, D, E, F
    }

    public class Printer : IDisposable
    {

        private bool disposed = false;
        protected bool send_at_dispose_if_not_yet_sent = false;
        protected int operation_start_index = 0;
        protected IntPtr printer = new IntPtr();
        protected List<byte[]> operations = new List<byte[]> { };
        public Barcode Barcode { get; }
        protected const string
            OPERATION_A = "\x02\x1b\x41", // STX + ESC + 'A'
            OPERATION_Z = "\x1b\x5a\x03"; // ESC + 'Z' + ETX
        const char ESC = '\x1b';

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        protected static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);
        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        protected static extern bool ClosePrinter(IntPtr hPrinter);
        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        protected static extern bool StartDocPrinter(IntPtr hPrinter, Int32 level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);
        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        protected static extern bool EndDocPrinter(IntPtr hPrinter);
        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        protected static extern bool StartPagePrinter(IntPtr hPrinter);
        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        protected static extern bool EndPagePrinter(IntPtr hPrinter);
        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        protected static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, Int32 dwCount, out Int32 dwWritten);

        public Printer(string PrinterName, bool send_at_dispose_if_not_yet_sent) : this(PrinterName)
        {
            this.send_at_dispose_if_not_yet_sent = send_at_dispose_if_not_yet_sent;
        }

        public Printer(string name)
        {
            if (!OpenPrinter(name.Normalize(), out printer, IntPtr.Zero))
                throw new TinySatoException("failed to use printer.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            this.Barcode = new Barcode(this);
        }

        public void MoveToX(int x)
        {
            if (!(1 <= x && x <= 9999))
                throw new TinySatoException("Specify 1-9999 dots.");
            Add(string.Format("H{0:D4}", x));
        }

        public void MoveToY(int y)
        {
            if (!(1 <= y && y <= 9999))
                throw new TinySatoException("Specify 1-9999 dots.");
            Add(string.Format("V{0:D4}", y));
        }

        public void SetGapSizeBetweenLabels(int y)
        {
            if (!(0 <= y && y <= 64))
                throw new TinySatoException("Specify 0-64 dots.");
            Add(string.Format("TG{0:D2}", y));
        }

        public void SetDensity(int density, DensitySpec spec)
        {
            if (!(1 <= density && density <= 5))
                throw new TinySatoException("Specify 1-5 density");
            Insert(0, OPERATION_A);
            Insert(1, string.Format("#E{0:D1}{1}", density, spec.ToString("F")));
            Insert(2, OPERATION_Z);
            operation_start_index += 3;
        }

        public void SetSpeed(int speed)
        {
            if (!(1 <= speed && speed <= 5))
                throw new TinySatoException("Specify 1-5 speed");
            Insert(0, OPERATION_A);
            Insert(1, string.Format("CS{0:D2}", speed));
            Insert(2, OPERATION_Z);
            operation_start_index += 3;
        }

        public void SetStartPosition(int x, int y)
        {
            if (!(Math.Abs(x) <= 9999))
                throw new TinySatoException("Specify -9999 <= x <= 9999 dots.");
            if (!(Math.Abs(y) <= 999))
                throw new TinySatoException("Specify -999 <= y <= 999 dots.");
            Add(string.Format("A3V{0:D4}H{1:D3}", y, x));
        }

        public void SetPaperSize(int height, int width)
        {
            if (!(1 <= height && height <= 9999))
                throw new TinySatoException("Specify 1-9999 dots for height.");
            if (!(1 <= width && width <= 9999))
                throw new TinySatoException("Specify 1-9999 dots for width.");
            Add(string.Format("A1{0:D4}{1:D4}", height, width));
        }

        public void SetCalendar(DateTime dt)
        {
            Add(string.Format("WT{0:D2}{1:D2}{2:D2}{3:D2}{4:D2}",
                dt.Year % 1000, dt.Month, dt.Day, dt.Hour, dt.Minute));
        }

        public void SetPageNumber(uint number_of_pages)
        {
            if (!(1 <= number_of_pages && number_of_pages <= 999999))
                throw new TinySatoException("Specify 1-999999 pages.");
            Add(string.Format("Q{0:D6}", number_of_pages));
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
                Add("GM" + string.Format("{0:D5}", bmp.Length) + ",");
                operations[operations.Count - 1] =
                    operations[operations.Count - 1].Concat(bmp).ToArray();
            }
        }

        public void AddGraphics(Bitmap original, bool is_strict = false)
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
                Add("GH" + string.Format("{0:D3}{1:D3}{2}",
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

        public void SetSensorType(SensorType type)
        {
            Add(string.Format("IG{0:D1}", (int)type));
        }

        public void Add(string operation)
        {
            operations.Add(Encoding.ASCII.GetBytes(ESC + operation));
        }

        protected void Insert(int index, string operation)
        {
            operations.Insert(
                index,
                Encoding.ASCII.GetBytes(ESC + operation));
        }

        public void Send(uint number_of_pages)
        {
            this.SetPageNumber(number_of_pages);
            Send();
        }

        public void Send()
        {
            operations.Insert(operation_start_index,
                Encoding.ASCII.GetBytes(OPERATION_A));
            operations.Add(Encoding.ASCII.GetBytes(OPERATION_Z));

            var flatten = operations.SelectMany(x => x).ToArray();
            var raw = Marshal.AllocCoTaskMem(flatten.Length);
            Marshal.Copy(flatten, 0, raw, flatten.Length);
            const int level = 1; // for not win98
            var di = new DOCINFOA();
            di.pDocName = "RAW DOCUMENT";
            di.pDataType = "raw";
            int written = 0;
            try
            {
                if (!StartDocPrinter(printer, level, di))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!StartPagePrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!WritePrinter(printer, raw, flatten.Length, out written))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!EndPagePrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!EndDocPrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                operation_start_index = 0;
                this.operations.Clear();
            }
            catch (Win32Exception inner)
            {
                throw new TinySatoException("failed to send operations.", inner);
            }
            finally { Marshal.FreeCoTaskMem(raw); }
        }

        public void Close()
        {
            if (printer != IntPtr.Zero && !ClosePrinter(printer))
            {
                var code = Marshal.GetLastWin32Error();
                var inner = new Win32Exception(code);
                throw new TinySatoException("failed to close printer.", inner);
            }
        }

        ~Printer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (this.operations.Count > 0 && send_at_dispose_if_not_yet_sent)
            {
                this.Send();
            }

            if (disposing)
            {
                this.operations.Clear();
            }
            this.Close();
            disposed = true;
        }
    }
}
