namespace TinySato
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    public partial class Printer : IDisposable
    {
        private bool disposed = false;
        protected bool send_at_dispose_if_not_yet_sent = false;
        protected int operation_start_index = 1;
        private IntPtr printer = IntPtr.Zero;
        protected List<byte[]> operations = new List<byte[]> {
            new byte[] { Convert.ToByte(STX) }
        };
        protected int soft_offset_x = 0;
        protected int soft_offset_y = 0;

        public Barcode Barcode { get; }
        public Graphic Graphic { get; }

        internal const char
            SOH = '\x01', STX = '\x02', ETX = '\x03', ENQ = '\x05', ESC = '\x1b';
        internal static readonly byte
            ASCII_SOH = Convert.ToByte(SOH), ASCII_STX = Convert.ToByte(STX),
            ASCII_ETX = Convert.ToByte(ETX),
            ASCII_ENQ = Convert.ToByte(ENQ), ASCII_ESC = Convert.ToByte(ESC);

        protected readonly string
            OPERATION_A = ESC + "A",
            OPERATION_Z = ESC + "Z";

        public Printer(string PrinterName, bool send_at_dispose_if_not_yet_sent) : this(PrinterName)
        {
            this.send_at_dispose_if_not_yet_sent = send_at_dispose_if_not_yet_sent;
        }

        public Printer(string name)
        {
            if (!UnsafeNativeMethods.OpenPrinter(name.Normalize(), out printer, IntPtr.Zero))
                throw new TinySatoException("failed to use printer.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            const int level = 1; // for not win98
            var di = new DOCINFO() { pDataType = "raw", pDocName = "RAW DOCUMENT" };
            if (!UnsafeNativeMethods.StartDocPrinter(printer, level, di))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            this.Barcode = new Barcode(this);
            this.Graphic = new Graphic(this);
        }

        public void MoveToX(int x)
        {
            var _x = x + soft_offset_x;
            if (!(1 <= _x && _x <= 9999))
                throw new TinySatoException("Specify 1-9999 dots.");
            Add(string.Format("H{0:D4}", _x));
        }

        public void MoveToY(int y)
        {
            var _y = y + soft_offset_y;
            if (!(1 <= _y && _y <= 9999))
                throw new TinySatoException("Specify 1-9999 dots.");
            Add(string.Format("V{0:D4}", _y));
        }

        public void SetGapSizeBetweenLabels(int y)
        {
            if (!(0 <= y && y <= 64))
                throw new TinySatoException("Specify 0-64 dots.");
            Insert(operation_start_index + 0, OPERATION_A);
            Insert(operation_start_index + 1, ESC + string.Format("TG{0:D2}", y));
            Insert(operation_start_index + 2, OPERATION_Z);
            operation_start_index += 3;
        }

        public void SetSpeed(int speed)
        {
            if (!(1 <= speed && speed <= 5))
                throw new TinySatoException("Specify 1-5 speed");
            Insert(operation_start_index + 0, OPERATION_A);
            Insert(operation_start_index + 1, ESC + string.Format("CS{0:D2}", speed));
            Insert(operation_start_index + 2, OPERATION_Z);
            operation_start_index += 3;
        }

        public void SetStartPosition(int x, int y)
        {
            if (!(Math.Abs(x) <= 999))
                throw new TinySatoException("Specify -999 <= x <= 999 dots.");
            if (!(Math.Abs(y) <= 999))
                throw new TinySatoException("Specify -999 <= y <= 999 dots.");
            Add(string.Format("A3V{0:+000;-000}H{1:+000;-000}", y, x));
        }

        public void SetStartPositionEx(int x, int y)
        {
            if (!(Math.Abs(x) <= 9999))
                throw new TinySatoException("Specify -9999 <= x <= 9999 dots.");
            if (!(Math.Abs(y) <= 9999))
                throw new TinySatoException("Specify -9999 <= y <= 9999 dots.");
            soft_offset_x = x;
            soft_offset_y = y;
        }

        public void SetPaperSize(int height, int width)
        {
            if (!(1 <= height && height <= 9999))
                throw new TinySatoException("Specify 1-9999 dots for height.");
            if (!(1 <= width && width <= 9999))
                throw new TinySatoException("Specify 1-9999 dots for width.");
            Insert(operation_start_index + 0, OPERATION_A);
            Insert(operation_start_index + 1, ESC + string.Format("A1{0:D4}{1:D4}", height, width));
            Insert(operation_start_index + 2, OPERATION_Z);
            operation_start_index += 3;
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

        public void Add(string operation)
        {
            operations.Add(Encoding.ASCII.GetBytes(ESC + operation));
        }

        internal void Add(byte[] raw_operation)
        {
            operations.Add(raw_operation);
        }

        protected void Insert(int index, string operation)
        {
            operations.Insert(
                index,
                Encoding.ASCII.GetBytes(operation));
        }

        /// <summary>
        /// Add another stream
        ///
        /// Add another empty stream for printing multi pages at once.
        /// </summary>
        public int AddStream()
        {
            operations.Insert(operation_start_index,
                Encoding.ASCII.GetBytes(OPERATION_A));
            operations.Add(Encoding.ASCII.GetBytes(OPERATION_Z));
            operation_start_index = operations.Count;

            var flatten = operations.SelectMany(x => x).ToArray();
            var raw = Marshal.AllocCoTaskMem(flatten.Length);
            Marshal.Copy(flatten, 0, raw, flatten.Length);
            int written = 0;
            try
            {
                if (!UnsafeNativeMethods.StartPagePrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!UnsafeNativeMethods.WritePrinter(printer, raw, flatten.Length, out written))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!UnsafeNativeMethods.EndPagePrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                this.operations.Clear();
                operation_start_index = 0;
            }
            catch (Win32Exception inner)
            {
                throw new TinySatoException("failed to send operations.", inner);
            }
            finally { Marshal.FreeCoTaskMem(raw); }
            return flatten.Length;
        }

        public int Send(uint number_of_pages)
        {
            this.SetPageNumber(number_of_pages);
            return Send();
        }

        public int Send()
        {
            operations.Insert(operation_start_index,
                Encoding.ASCII.GetBytes(OPERATION_A));
            operations.Add(Encoding.ASCII.GetBytes(OPERATION_Z + ETX));

            var flatten = operations.SelectMany(x => x).ToArray();
            var raw = Marshal.AllocCoTaskMem(flatten.Length);
            Marshal.Copy(flatten, 0, raw, flatten.Length);
            int written = 0;
            try
            {
                if (!UnsafeNativeMethods.StartPagePrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!UnsafeNativeMethods.WritePrinter(printer, raw, flatten.Length, out written))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!UnsafeNativeMethods.EndPagePrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                this.operations.Clear();
                this.operations.Add(new byte[] { Convert.ToByte(STX) });
                operation_start_index = this.operations.Count();
            }
            catch (Win32Exception inner)
            {
                throw new TinySatoException("failed to send operations.", inner);
            }
            finally { Marshal.FreeCoTaskMem(raw); }
            return flatten.Length;
        }

        public void Close()
        {
            if (printer != IntPtr.Zero && !UnsafeNativeMethods.EndDocPrinter(printer))
            {
                var code = Marshal.GetLastWin32Error();
                var inner = new Win32Exception(code);
                throw new TinySatoException("failed to end document.", inner);
            }
            if (printer != IntPtr.Zero && !UnsafeNativeMethods.ClosePrinter(printer))
            {
                var code = Marshal.GetLastWin32Error();
                var inner = new Win32Exception(code);
                throw new TinySatoException("failed to close printer.", inner);
            }
            printer = IntPtr.Zero;
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
                this.Close();
                this.operations.Clear();
            }
            disposed = true;
        }
    }
}
