namespace UnitTestProject
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Printing;
    using System.Text;
    using TinySato;

    [TestClass]
    public class UnitTest1 : IDisposable
    {
        protected const string printer_name = "T408v";
        protected const string printer_ip = "127.0.0.1";
        protected const int printer_port = 9100;
        protected const byte STX = 0x02, ETX = 0x03, ESC = 0x1b;
        protected TcpListener listener;

        const double inch2mm = 25.4;
        const double dpi = 203;
        const double dot2mm = inch2mm / dpi; // 25.4(mm) / 203(dot) -> 0.125(mm/dot)
        const double mm2dot = 1 / dot2mm; // 8(dot/mm)

        public UnitTest1()
        {
            listener = new TcpListener(
                IPAddress.Parse(printer_ip), printer_port) { ExclusiveAddressUse = true };
            listener.Start();
        }

        public void Dispose()
        {
            listener.Stop();
        }

        protected byte[] GetBinary()
        {
            var client = listener.AcceptTcpClient();
            var buffer = new List<byte[]>();
            using (var stream = client.GetStream())
            {
                var raw = new byte[1024];
                int ret;
                while ((ret = stream.Read(raw, 0, raw.Length)) != 0)
                {
                    var dest = new byte[ret];
                    Array.Copy(raw, dest, ret);
                    buffer.Add(dest);
                }
            }
            client.Close();
            return buffer.SelectMany(x => x).ToArray();
        }

        protected int GetJobCount()
        {
            using (var server = new LocalPrintServer())
            using (var queue = server.GetPrintQueue(printer_name))
            {
                return queue.NumberOfJobs;
            }
        }

        protected int GetLastJobPageCount()
        {
            using (var server = new LocalPrintServer())
            using (var queue = server.GetPrintQueue(printer_name))
            using (var jobs = queue.GetPrintJobInfoCollection())
            {
                var last = jobs.OrderBy(job => job.TimeJobSubmitted).Last();
                return last.NumberOfPages;
            }
        }

        [TestCleanup]
        public void TearDown()
        {
            using (var server = new LocalPrintServer())
            using (var queue = server.GetPrintQueue(printer_name))
            {
                while (true)
                {
                    using (var jobs = queue.GetPrintJobInfoCollection())
                    {
                        if (jobs.Count() == 0)
                        {
                            break;
                        }

                        foreach (var job in jobs)
                        {
                            if (job.IsPrinting)
                            {
                                job.Cancel();
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void MultiJob()
        {
            var before = GetJobCount();
            var now = DateTime.Now;
            var first = new Printer(printer_name, true);
            var second = new Printer(printer_name);
            Assert.AreEqual(before + 2, GetJobCount());

            // send before first job, but block until first job ending.
            second.Send(); // send <A><Z>, job +1
            first.SetCalendar(now); // To add job, need a operation at least.
            // safe to Dispose, Close even if multiple times
            first.Dispose();
            first.Dispose();
            second.Close();
            second.Close();

            var actual1 = GetBinary();
            var expected1 = new string[]
            {
                "A",
                string.Format("WT{0:D2}{1:D2}{2:D2}{3:D2}{4:D2}",
                    now.Year % 1000, now.Month, now.Day, now.Hour, now.Minute),
                "Z",
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x))).ToList();
            expected1.Insert(0, STX);
            expected1.Add(ETX);

            var actual2 = GetBinary();
            var expected2 = new string[]
            {
                "A",
                "Z",
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x))).ToList();
            expected2.Insert(0, STX);
            expected2.Add(ETX);

            CollectionAssert.AreEqual(
                expected1.Concat(expected2).ToArray(),
                actual1.Concat(actual2).ToArray());
        }

        [TestMethod]
        public void MultiLabelAtSingleJob()
        {
            var before = GetJobCount();
            using (var printer = new Printer(printer_name))
            {
                // page 1
                printer.Barcode.AddCODE128(1, 2, "HELLO");
                printer.SetPageNumber(3);
                printer.AddStream();
                // page 2
                printer.Barcode.AddCODE128(4, 5, "WORLD");
                printer.SetPageNumber(6);
                printer.AddStream();
                // page 3
                printer.Barcode.AddCODE128(7, 8, "!!!");
                printer.Send(9);
                // page 4 (empty page)
                printer.Send(1);
            }
            Assert.AreEqual(before + 1, GetJobCount(), "Job count");
            Assert.AreEqual(4, GetLastJobPageCount(), "Variation of page");

            var expected = new string[]
            {
                "A",
                "BG" + "01" + "002" + "HELLO",
                "Q" + "000003",
                "Z",
                "A",
                "BG" + "04" + "005" + "WORLD",
                "Q" + "000006",
                "Z",
                "A",
                "BG" + "07" + "008" + "!!!",
                "Q" + "000009",
                "Z",
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x))).ToList();
            expected.Insert(0, STX);
            expected.Add(ETX);

            // empty page
            var expected_empty = new string[]
            {
                "A",
                "Q" + "000001",
                "Z",
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x))).ToList();
            expected_empty.Insert(0, STX);
            expected_empty.Add(ETX);
            var actual = GetBinary();
            CollectionAssert.AreEqual(expected.Concat(expected_empty).ToArray(), actual);
        }

        [TestMethod]
        public void ExampleBarcode()
        {
            var before = GetJobCount();
            var barcode = "1234567890128";

            using (var sato = new Printer(printer_name))
            {
                sato.SetSensorType(SensorType.Reflection);
                sato.SetGapSizeBetweenLabels((int)Math.Round(2.0 * mm2dot));
                sato.SetDensity(3, DensitySpec.A);
                sato.SetSpeed(4);
                sato.SetPaperSize((int)(80 * mm2dot), (int)(104 * mm2dot));
                sato.SetStartPosition(0, 0);

                sato.MoveToX(80);
                sato.MoveToY(80);
                sato.Barcode.AddJAN13(3, 70, barcode);
                sato.MoveToX(8);
                sato.MoveToY(8);
                sato.Barcode.AddCodabar(1, 2, barcode);

                sato.SetPageNumber(1);
                sato.Send();
            }

            var after = GetJobCount();
            Assert.AreEqual(before + 1, after);

            var expected = new string[] {
                "A",
                "IG" + "0",
                "Z",
                "A",
                "TG16",
                "Z",
                "A",
                "#E3A",
                "Z",
                "A",
                "CS04",
                "Z",
                "A",
                "A1" + "0639" + "0831",
                "Z",
                "A",
                "A3V+000H+000",
                "H" + "0080",
                "V" + "0080",
                "BD" + "3" + "03" + "070" + "1234567890128",
                "H" + "0008",
                "V" + "0008",
                "B" + "0" + "01" + "002" + "A1234567890128A",
                "Q" + "000001",
                "Z"
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x))).ToList();
            expected.Insert(0, STX);
            expected.Add(ETX);
            var actual = GetBinary();
            CollectionAssert.AreEqual(expected.ToArray(), actual);
        }

        [TestMethod]
        public void ExampleGraphic()
        {
            var before = GetJobCount();
            using (var sato = new Printer(printer_name))
            {
                int width = (int)Math.Round(85.0 * mm2dot),
                    height = (int)Math.Round(50.0 * mm2dot);

                sato.MoveToX((int)Math.Round(1.3 * mm2dot));
                sato.MoveToY(1);
                sato.Graphic.AddBox(
                    (int)Math.Round(0.5 * mm2dot),
                    (int)Math.Round(0.5 * mm2dot),
                    width,
                    height);

                using (var bitmap = (Bitmap)Image.FromFile("input.png"))
                {
                    sato.MoveToX((int)Math.Round(1.3 * mm2dot));
                    sato.MoveToY(1);
                    sato.Graphic.AddGraphic(bitmap);
                    sato.MoveToX(1);
                    sato.MoveToY(1);
                    sato.Graphic.AddBitmap(bitmap);
                }
                sato.SetPageNumber(1);
                sato.Send();
            }
            var after = GetJobCount();
            Assert.AreEqual(before + 1, after, "Job count");

            var _expected = new string[] {
                "A",
                "H0010",
                "V0001",
                "FW0404V0400H0679",
                "H0010",
                "V0001",
                "GH108067" + File.ReadAllText("output.txt"),
                "H0001",
                "V0001",
                string.Format("GM{0},", new FileInfo("output.bmp").Length),
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x)));
            _expected = _expected.Concat(File.ReadAllBytes("output.bmp"));
            _expected = _expected.Concat(new byte[] { ESC }.Concat(Encoding.ASCII.GetBytes("Q000001")));

            var expected = new byte[] { STX }.Concat(_expected).Concat(new byte[] { ESC, Convert.ToByte('Z'), ETX });
            var actual = GetBinary();
            CollectionAssert.AreEqual(expected.ToArray(), actual);
        }
    }
}
