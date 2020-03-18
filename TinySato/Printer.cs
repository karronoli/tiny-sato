namespace TinySato
{
    using Communication;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    public partial class Printer : IDisposable
    {
        private bool disposed = false;

        private IntPtr printer = IntPtr.Zero;

        static readonly TimeSpan ConnectWaitTimeout = TimeSpan.FromSeconds(3);
        static readonly TimeSpan PrintSendTimeout = TimeSpan.FromMilliseconds(200); // CT408i driver default setting
        private TcpClient client;

        protected int operation_start_index = 1;
        protected List<byte[]> operations = new List<byte[]> {
            new byte[] { Convert.ToByte(STX) }
        };

        public Barcode Barcode { get; }
        public Graphic Graphic { get; }
        public ConnectionType ConnectionType { get; }

        internal const char
            SOH = '\x01', STX = '\x02', ETX = '\x03', ENQ = '\x05', ESC = '\x1b';
        internal static readonly byte
            ASCII_SOH = Convert.ToByte(SOH), ASCII_STX = Convert.ToByte(STX),
            ASCII_ETX = Convert.ToByte(ETX),
            ASCII_ENQ = Convert.ToByte(ENQ), ASCII_ESC = Convert.ToByte(ESC);

        protected readonly string
            OPERATION_A = ESC + "A",
            OPERATION_Z = ESC + "Z";

        protected JobStatus status;

        public Printer(string name)
        {
            this.ConnectionType = ConnectionType.Driver;
            this.Barcode = new Barcode(this);
            this.Graphic = new Graphic(this);

            if (!UnsafeNativeMethods.OpenPrinter(name.Normalize(), out printer, IntPtr.Zero))
                throw new TinySatoPrinterNotFoundException($"The printer not found. name:{name}",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            const int level = 1; // for not win98
            var di = new DOCINFO() { pDataType = "raw", pDocName = "RAW DOCUMENT" };
            if (!UnsafeNativeMethods.StartDocPrinter(printer, level, di))
                throw new TinySatoIOException($"Failed to use printer. name:{name}",
                    new Win32Exception(Marshal.GetLastWin32Error()));
        }

        public Printer(IPEndPoint endpoint)
        {
            this.ConnectionType = ConnectionType.IP;
            this.Barcode = new Barcode(this);
            this.Graphic = new Graphic(this);

            this.client = new TcpClient() { SendTimeout = PrintSendTimeout.Milliseconds };
            try
            {
                this.client.Connect(endpoint);
            }
            catch (SocketException e)
            {
                throw new TinySatoPrinterNotFoundException($"The printer is maybe none in the same network. endpoint: {endpoint}", e);
            }

            this.status = new JobStatus(client.GetStream());
            if (!status.OK)
            {
                var timer = Stopwatch.StartNew();
                while (ConnectWaitTimeout > timer.Elapsed)
                {
                    Task.Delay(TimeSpan.FromMilliseconds(100)).Wait();
                    this.status = status.Refresh();
                    if (status.OK) break;
                }
                if (!status.OK)
                    throw new TinySatoIOException($"Printer is busy. endpoint: {endpoint}, status: {status}");
            }
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
            this.Send(flatten);
            this.operations.Clear();
            operation_start_index = 0;

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
            this.Send(flatten);
            this.operations.Clear();
            this.operations.Add(new byte[] { Convert.ToByte(STX) });
            operation_start_index = this.operations.Count();

            return flatten.Length;
        }

        void Send(byte[] raw)
        {
            if (printer != IntPtr.Zero) SendWin32(raw);
            else if (client != null) SendTcp(raw);
            else throw new NotImplementedException();
        }

        void SendWin32(byte[] raw)
        {
            var ptr = Marshal.AllocCoTaskMem(raw.Length);
            Marshal.Copy(raw, 0, ptr, raw.Length);

            try
            {
                if (!UnsafeNativeMethods.StartPagePrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!UnsafeNativeMethods.WritePrinter(printer, ptr, raw.Length, out int written))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!UnsafeNativeMethods.EndPagePrinter(printer))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            catch (Win32Exception e)
            {
                throw new TinySatoIOException("failed to send operations for windows printer.", e);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
        }

        void SendTcp(byte[] raw)
        {
            try
            {
                this.status = status.Refresh();
                if (!status.OK)
                    throw new TinySatoPrinterUnitException($"Printer is failure. {status}");
                client.Client.Send(raw);
            }
            catch (SocketException e)
            {
                throw new TinySatoIOException("failed to send operations by tcp.", e);
            }
        }

        public void Close()
        {
            if (client != null)
            {
                client.Close();
            }
            if (printer != IntPtr.Zero && !UnsafeNativeMethods.EndDocPrinter(printer))
            {
                var code = Marshal.GetLastWin32Error();
                var inner = new Win32Exception(code);
                throw new TinySatoIOException("failed to end document.", inner);
            }
            if (printer != IntPtr.Zero && !UnsafeNativeMethods.ClosePrinter(printer))
            {
                var code = Marshal.GetLastWin32Error();
                var inner = new Win32Exception(code);
                throw new TinySatoIOException("failed to close printer.", inner);
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

            if (disposing)
            {
                this.Close();
                if (client != null) client.Dispose();
            }
            disposed = true;
        }
    }
}
