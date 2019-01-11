namespace UnitTestProject
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using TinySato;
    using TinySato.Search;

    [TestClass]
    public class UnitTest1
    {
        const string printer_mac = "02:00:00:00:00:01";
        static readonly string printer_mac_eui48 = printer_mac.Replace(':', '-').ToUpper();

        const byte SOH = 0x01, STX = 0x02, ETX = 0x03, ENQ = 0x05, ESC = 0x1b;
        const byte ASCII_A = 0x41, ASCII_L = 0x4c, ASCII_Z = 0x5a;

        static readonly byte[] SearchResponseBody = File.ReadAllBytes("search-response.bin");
        static readonly byte[] HealthOKBody = File.ReadAllBytes("health-ok.bin");

        static readonly IPEndPoint searchEP = new IPEndPoint(IPAddress.Any, 19541);
        static readonly IPEndPoint printEP = new IPEndPoint(IPAddress.Any, 9100);
        static TcpListener listener;

        const double inch2mm = 25.4;
        const double dpi = 203;
        const double dot2mm = inch2mm / dpi; // 25.4(mm) / 203(dot) -> 0.125(mm/dot)
        const double mm2dot = 1 / dot2mm; // 8(dot/mm)

        [TestInitialize]
        public void Listen()
        {
            listener = new TcpListener(printEP) { ExclusiveAddressUse = true };
            listener.Start(1);
        }

        [TestCleanup]
        public void Stop()
        {
            listener.Stop();
        }

        protected async static Task<byte[]> ResponseForPrint()
        {
            var buffers = new List<byte[]>();

            using (var client = await listener.AcceptTcpClientAsync())
            using (var stream = client.GetStream())
            {
                while (true)
                {
                    var dummy = new byte[client.ReceiveBufferSize];
                    var actual_buffer_length = await stream.ReadAsync(dummy, 0, dummy.Length);
                    var buffer = dummy.Take(actual_buffer_length);

                    if (buffer.Count() == 0)
                    {
                        var last = buffers.Last();
                        if (last.Last() == ETX) break;
                    }

                    if (buffer.Last() == ENQ)
                        await stream.WriteAsync(HealthOKBody, 0, HealthOKBody.Length);

                    buffers.Add(buffer.ToArray());
                }

                return buffers.SelectMany(buffer => buffer.Where(b => b != ENQ)).ToArray();
            }
        }

        protected async static Task<byte[]> ResponseForSearch()
        {
            UdpReceiveResult result;
            using (var server = new UdpClient(searchEP) { EnableBroadcast = true })
            {
                server.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                result = await server.ReceiveAsync();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));

            using (var client = new UdpClient() { EnableBroadcast = true })
            {
                client.Connect(new IPEndPoint(IPAddress.Broadcast, result.RemoteEndPoint.Port));
                await client.SendAsync(SearchResponseBody, SearchResponseBody.Length);
            }

            return result.Buffer;
        }

        [TestMethod]
        public async Task SearchPrinter()
        {
            var task = ResponseForSearch();
            var wait_time = TimeSpan.FromMilliseconds(500);

            Response response = null;
            using (task)
            {
                var mac = PhysicalAddress.Parse(printer_mac_eui48);
                Printer.ClearSearchCache();
                var responses = Printer.Search(wait_time).Where(r => r.MACAddress.Equals(mac));
                Assert.IsInstanceOfType(responses, typeof(IEnumerable<Response>));
                CollectionAssert.AreEqual(new byte[] { SOH, ASCII_L, ASCII_A }, await task);
                Assert.AreEqual(1, responses.Count());
                response = responses.First();
            }

            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), response.IPAddress);
            Assert.AreEqual(IPAddress.Parse("255.0.0.0"), response.SubnetMask);
            Assert.AreEqual(IPAddress.Parse("0.0.0.0"), response.Gateway);
            Assert.AreEqual("Lesprit Series", response.Name);
            Assert.IsTrue(response.DHCP);
            Assert.IsTrue(response.RARP);
        }

        [TestMethod]
        [ExpectedException(typeof(TinySatoException))]
        public void BusyPrinter()
        {
            listener.Stop();
            var task = ResponseForSearch();
            Printer.ClearSearchCache();
            using (task)
            using (var printer = Printer.Find(printer_mac)) { }
        }

        [TestMethod]
        public async Task MultiLabel()
        {
            var task = ResponseForSearch();
            var task2 = ResponseForPrint();

            Printer.ClearSearchCache();

            using (task)
            using (var printer = Printer.Find(printer_mac))
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
                printer.SetPageNumber(9);
                printer.Send();
            }

            var expected = new string[]
            {
                // page 1
                "A",
                "BG" + "01" + "002" + "HELLO",
                "Q" + "000003",
                "Z",

                // page 2
                "A",
                "BG" + "04" + "005" + "WORLD",
                "Q" + "000006",
                "Z",

                // page 3
                "A",
                "BG" + "07" + "008" + "!!!",
                "Q" + "000009",
                "Z",
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x)))
            .Prepend(STX).Append(ETX);

            using (task2)
            {
                var actual = await task2;
                CollectionAssert.AreEqual(expected.ToArray(), actual);
            }
        }

        [TestMethod]
        public async Task ExampleBarcode()
        {
            var barcode = "1234567890128";
            var task = ResponseForSearch();
            var task2 = ResponseForPrint();
            Printer.ClearSearchCache();

            using (task)
            using (var printer = Printer.Find(printer_mac))
            {
                Assert.IsInstanceOfType(printer, typeof(Printer));
                Assert.AreEqual(ConnectionType.IP, printer.ConnectionType);

                // settings
                printer.SetSensorType(SensorType.Reflection);
                printer.SetGapSizeBetweenLabels((int)Math.Round(2.0 * mm2dot));
                printer.SetDensity(3, DensitySpec.A);
                printer.SetSpeed(4);
                printer.SetPaperSize((int)(80 * mm2dot), (int)(104 * mm2dot));
                printer.SetStartPosition(0, 0);

                // drawings
                printer.MoveToX(80);
                printer.MoveToY(80);
                printer.Barcode.AddJAN13(3, 70, barcode);
                printer.MoveToX(8);
                printer.MoveToY(8);
                printer.Barcode.AddCodabar(1, 2, barcode);

                // print
                printer.SetPageNumber(1);
                printer.Send();
            }

            IEnumerable<byte> expected = new string[]
            {
                // settings
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

                // drawings
                "H" + "0080",
                "V" + "0080",
                "BD" + "3" + "03" + "070" + barcode,
                "H" + "0008",
                "V" + "0008",
                "B" + "0" + "01" + "002" + "A" + barcode + "A",

                // print
                "Q" + "000001",
                "Z"
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x)))
            .Prepend(STX).Append(ETX);

            using (task2)
            {
                var actual = await task2;
                CollectionAssert.AreEqual(expected.ToArray(), actual);
            }
        }

        [TestMethod]
        public async Task ExampleGraphic()
        {
            var task = ResponseForSearch();
            var task2 = ResponseForPrint();
            Printer.ClearSearchCache();

            using (task)
            using (var printer = Printer.Find(printer_mac))
            {
                int width = (int)Math.Round(85.0 * mm2dot),
                    height = (int)Math.Round(50.0 * mm2dot);

                // box
                printer.MoveToX((int)Math.Round(1.3 * mm2dot));
                printer.MoveToY(1);
                printer.Graphic.AddBox(
                    (int)Math.Round(0.5 * mm2dot),
                    (int)Math.Round(0.5 * mm2dot),
                    width,
                    height);

                using (var bitmap = (Bitmap)Image.FromFile("input.png"))
                {
                    // graphic
                    printer.MoveToX((int)Math.Round(1.3 * mm2dot));
                    printer.MoveToY(1);
                    printer.Graphic.AddGraphic(bitmap);

                    // bitmap
                    printer.MoveToX(1);
                    printer.MoveToY(1);
                    printer.Graphic.AddBitmap(bitmap);
                }
                // print
                printer.SetPageNumber(1);
                printer.Send();
            }

            IEnumerable<byte> expected = new string[]
            {
                "A",

                // box
                "H0010",
                "V0001",
                "FW0404V0400H0679",

                // graphic
                "H0010",
                "V0001",
                "GH108067" + File.ReadAllText("output.txt"),

                // bitmap
                "H0001",
                "V0001",
                string.Format("GM{0},", new FileInfo("output.bmp").Length),
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x)))
            .Concat(File.ReadAllBytes("output.bmp"))
            // print
            .Append(ESC).Concat(Encoding.ASCII.GetBytes("Q000001"))
            .Concat(new byte[] { ESC, ASCII_Z })
            .Prepend(STX).Append(ETX);

            using (task2)
            {
                var actual = await task2;
                CollectionAssert.AreEqual(expected.ToArray(), actual);
            }
        }
    }
}
