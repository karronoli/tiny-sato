using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;

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
        protected bool is_sent = false;
        protected IntPtr printer = new IntPtr();
        protected List<byte[]> operations = new List<byte[]> { };
        const byte STX = 0x02, ESC = 0x1b, ETX = 0x03;

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);
        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);
        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, Int32 level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);
        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);
        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);
        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);
        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, Int32 dwCount, out Int32 dwWritten);

        public Printer(string PrinterName, bool send_at_dispose_if_not_yet_sent) : this(PrinterName)
        {
            this.send_at_dispose_if_not_yet_sent = send_at_dispose_if_not_yet_sent;
        }

        public Printer(string name)
        {
            if (!OpenPrinter(name.Normalize(), out printer, IntPtr.Zero))
                throw new TinySatoException("failed to use printer.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
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

        public void StartPointCorrection(int x, int y)
        public void SetDensity(int density, DensitySpec spec)
        {
            if (!(1 <= density && density <= 5))
                throw new TinySatoException("Specify 1-5 density");
            Add(string.Format("#E{0:D1}", density, spec.ToString("F")));
        }

        public void SetSpeed(int speed)
        {
            if (!(1 <= speed && speed <= 5))
                throw new TinySatoException("Specify 1-5 speed");
            Add(string.Format("CS{0:D2}", speed));
        }

        {
            if (!(1 <= x && x <= 9999))
                throw new TinySatoException("Specify 1-9999 dots for x position.");
            if (!(1 <= y && y <= 999))
                throw new TinySatoException("Specify 1-999 dots for y position.");
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

        public void AddBarCode128(int narrow_bar_width, int barcode_height, string print_data)
        {
            if (!(1 <= narrow_bar_width && narrow_bar_width <= 12))
                throw new TinySatoException("Specify 1-12 dot for Narrow Bar Width.");
            if (!(1 <= barcode_height && barcode_height <= 600))
                throw new TinySatoException("Specify 1-600 dot for Barcode Height.");
            Add(string.Format("BG{0:D2}{1:D3}{2}", narrow_bar_width, barcode_height, print_data));
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

        public void SetSensorType(SensorType type)
        {
            Add(string.Format("IG{0:D1}", (int)type));
        }

        public void Add(string operation)
        {
            var tmp = new byte[] { ESC }
                .Concat(System.Text.Encoding.ASCII.GetBytes(operation))
                .ToArray();
            operations.Add(tmp);
        }

        public void Send()
        {
            operations.Insert(0, new byte[]
            {
                // データ送信の開始を設定します
                STX, ESC, Convert.ToByte('A')
            });
            operations.Add(new byte[]
            {
                // データ送信の終了を設定します
                ESC, Convert.ToByte('Z'), ETX
            });

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
                is_sent = true;
            }
            catch (Win32Exception inner)
            {
                throw new TinySatoException("failed to send operations.", inner);
            }
            finally { Marshal.FreeCoTaskMem(raw); }
        }

        public void Close()
        {
            if (!ClosePrinter(printer))
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

            if (!is_sent && send_at_dispose_if_not_yet_sent)
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

    public class TinySatoException : Exception
    {
        public TinySatoException() { }

        public TinySatoException(string message)
            : base(message) { }

        public TinySatoException(string message, Exception inner)
            : base(message, inner) { }
    }
}
