using System;
using System.Collections.Generic;
using System.Linq;

namespace TinySato
{
    public enum Barcodes
    {
        CODE39 = 1,
        JAN13 = 3,
        EAN13 = 3
    }
    public class TinySato : IDisposable
    {
        private bool disposed = false;
        protected IntPtr printer = new IntPtr();
        protected List<byte[]> operations = new List<byte[]> { };
        const byte STX = 0x02, ESC = 0x1b, ETX = 0x03;

        public TinySato(string PrinterName)
        {
            string error = String.Empty;
            string job_name = "document";
            if (!Print.SatoOpen(ref printer, PrinterName, job_name, ref error))
            {
                throw new TinySatoException(error);
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

        public void AddBarCodeRatio12(Barcodes type, int zoom, int height, string str)
        {
            Add(string.Format(
                "D{0:D1}{1:D2}{2:D3}*{3}*",
                type, zoom, height, str));
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
            RawSend(operations.SelectMany(x => x).ToArray());
        }

        public void RawSend(byte[] operations)
        {
            string error = String.Empty;
            if (!Print.SatoSend(printer, operations, ref error))
            {
                throw new TinySatoException(error);
            }
        }

        public void Close()
        {
            string error = String.Empty;
            if (!Print.SatoClose(printer, ref error))
            {
                throw new TinySatoException(error);
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
