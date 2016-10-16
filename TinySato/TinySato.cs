using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;

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

    public class TinySato : IDisposable
    {
        public enum SensorType
        {
            Reflection = 0,
            Transparent = 1,
            Ignore = 2
        }

        private bool disposed = false;
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

        public TinySato(string PrinterName, string job_name = "")
        {
            try
            {
                if (!OpenPrinter(PrinterName.Normalize(), out printer, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                const int level = 1; // for not win98
                var di = new DOCINFOA();
                di.pDocName = string.IsNullOrEmpty(job_name) ? "RAW DOCUMENT" : job_name;
                di.pDataType = "raw";
                if (!StartDocPrinter(printer, level, di))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!StartPagePrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            catch (Win32Exception inner)
            {
                throw new TinySatoException("failed to use printer.", inner);
            }
        }

        public void MoveToX(int x)
        {
            Add(string.Format("H{0:D4}", x));
        }

        public void MoveToY(int y)
        {
            Add(string.Format("V{0:D4}", y));
        }

        public void StartPointCorrection(int x, int y)
        {
            Add(string.Format("A3V{0:D4}H{1:D3}", y, x));
        }

        public void SetPaperSize(int height, int width)
        {
            Add(string.Format("A1V{0:D5}H{1:D4}", height, width));
        }

        public void SetCalendar(DateTime dt)
        {
            Add(string.Format("WT{0:D2}{1:D2}{2:D2}{3:D2}{4:D2}",
                dt.Year % 1000, dt.Month, dt.Day, dt.Hour, dt.Minute));
        }

        public void SetPageNumber(uint number_of_pages)
        {
            if (!(1 <= number_of_pages && number_of_pages <= 999999))
                throw new TinySatoException("Specify 1 or more for number of pages!");
            Add(string.Format("Q{0:D6}", number_of_pages));
        }

        public void AddBarCode128(int narrow_bar_width, int barcode_height, string print_data)
        {
            Add(string.Format("BG{0:D2}{1:D3}{2}", narrow_bar_width, barcode_height, print_data));
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
            try
            {
                int written = 0;
                if (!WritePrinter(printer, raw, flatten.Length, out written))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!EndPagePrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!EndDocPrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
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

        ~TinySato()
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
