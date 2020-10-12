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
    using System.Threading;
    using System.Threading.Tasks;

    public partial class Printer : IDisposable
    {
        private bool disposed = false;

        private IntPtr printer = IntPtr.Zero;
        readonly TcpClient client;

        static readonly TimeSpan ConnectWaitTimeout = TimeSpan.FromSeconds(30);
        static readonly TimeSpan ConnectWaitInterval = TimeSpan.FromMilliseconds(100);
        static readonly TimeSpan PrintSendInterval = TimeSpan.FromMilliseconds(200); // CT408i driver default setting

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

        static readonly string
            OPERATION_A = ESC + "A",
            OPERATION_Z = ESC + "Z";

        protected JobStatus status;

        public Printer(string name, string DocName = "RAW DOCUMENT")
        {
            if (string.IsNullOrEmpty(DocName))
            {
                throw new TinySatoArgumentException($"The document name is empty. name:{name}, DocName:{DocName}");
            }

            this.ConnectionType = ConnectionType.Driver;
            this.Barcode = new Barcode(this);
            this.Graphic = new Graphic(this);

            if (!UnsafeNativeMethods.OpenPrinter(name, out printer, IntPtr.Zero))
                throw new TinySatoPrinterNotFoundException($"The printer not found. name:{name}",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            const int level = 1; // for not win98
            var di = new DOCINFO() { pDataType = "raw", pDocName = DocName };
            if (!UnsafeNativeMethods.StartDocPrinter(printer, level, di))
                throw new TinySatoIOException($"Failed to use printer. name:{name}",
                    new Win32Exception(Marshal.GetLastWin32Error()));

            this.status = new JobStatus(ConnectionType.Driver);
        }

        public Printer(IPEndPoint endpoint)
        {
            this.ConnectionType = ConnectionType.IP;
            this.Barcode = new Barcode(this);
            this.Graphic = new Graphic(this);

            this.client = new TcpClient()
            {
                SendTimeout = (int)PrintSendInterval.TotalMilliseconds,
                NoDelay = true
            };
            var timer = Stopwatch.StartNew();
            using (var task = this.client.ConnectAsync(endpoint.Address, endpoint.Port))
            {
                try
                {
                    if (!task.Wait(ConnectWaitTimeout))
                    {
                        throw new TinySatoIOException($"The printer is maybe none in the same network. endpoint: {endpoint}");
                    }
                }
                catch (AggregateException e)
                {
                    throw new TinySatoPrinterNotFoundException($"The printer is bad network status. endpoint: {endpoint}", e.InnerException);
                }

                this.status = new JobStatus(this.client.GetStream());
                while (!this.status.OK && ConnectWaitTimeout > timer.Elapsed)
                {
                    Task.Delay(ConnectWaitInterval).Wait();
                    this.status = this.status.Refresh();
                }
            }

            if (!this.status.OK)
                throw new TinySatoIOException($"Printer is busy. endpoint: {endpoint}, status: {this.status}");
        }

        internal void Add(string operation)
        {
            operations.Add(Encoding.ASCII.GetBytes(ESC + operation));
        }

        internal void Add(byte[] raw_operation)
        {
            operations.Add(raw_operation);
        }

        public void PushOperation(IEnumerable<byte> operation)
        {
            operations.Add(operation.ToArray());
        }

        public byte[] PopOperation()
        {
            var last = operations.Last();
            operations.RemoveAt(operations.Count - 1);

            return last;
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
        public async Task<int> AddStreamAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ConnectionType == ConnectionType.Driver)
                return AddStreamInternal();

            if (ConnectionType != ConnectionType.IP)
                throw new NotImplementedException();

            var sent = AddStreamInternal();

            this.status = new JobStatus(this.client.GetStream());
            for (; !this.status.OK; this.status = this.status.Refresh())
            {
                token.ThrowIfCancellationRequested();
                // No token without TaskCanceledException
                await Task.Delay(PrintSendInterval);
            }

            return sent;
        }

        public int AddStream() => AddStreamInternal();

        int AddStreamInternal()
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

        public async Task<int> SendAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ConnectionType == ConnectionType.Driver)
                return this.SendInternal();

            if (ConnectionType != ConnectionType.IP)
                throw new NotImplementedException();

            var sent1 = this.AddStreamInternal();

            this.status = new JobStatus(this.client.GetStream());
            for (; !this.status.OK; this.status = this.status.Refresh())
            {
                token.ThrowIfCancellationRequested();
                // No token without TaskCanceledException
                await Task.Delay(PrintSendInterval);
            }

            var sent2 = this.SendInternal();

            return sent1 + sent2;
        }


        public int Send() => SendInternal();

        int SendInternal()
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
            switch (ConnectionType)
            {
                case ConnectionType.Driver:
                    SendWin32(raw);
                    break;
                case ConnectionType.IP:
                    SendTcp(raw);
                    break;
                default:
                    throw new NotImplementedException();
            }
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
                throw new TinySatoIOException($"Failed to send operations for windows printer. ErrorCode: {e.ErrorCode}", e);
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
                client.Client.Send(raw);
            }
            catch (SocketException e)
            {
                throw new TinySatoIOException($"Failed to send operations. endpoint: {client.Client.RemoteEndPoint}, ErrorCode: {e.ErrorCode}", e);
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
