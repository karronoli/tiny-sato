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
    using System.Text;
    using System.Threading.Tasks;
    using TinySato;

    [TestClass]
    public class UnitTest1
    {
        const byte NULL = 0x0, STX = 0x02, ETX = 0x03, ENQ = 0x05, ESC = 0x1b, FS = 0x1c;
        const byte ASCII_SPACE = 0x20, ASCII_ZERO = 0x30, ASCII_Z = 0x5a;

        // STATUS4 standard http://www.sato.co.jp/webmanual/printer/cl4nx-j_cl6nx-j/main/main_GUID-D94C3DAD-1A55-4706-A86D-71EF71C6F3E3.html#
        static readonly byte[] HealthOKBody = new byte[]
        {
            NULL, NULL, NULL, FS,
            ENQ,
            STX,

            // JobStatus.ID
            ASCII_SPACE, ASCII_SPACE,

            // JobStatus.Health = State.Online
            Convert.ToByte('A'),

            // JobStatus.LabelRemaining = 0
            ASCII_ZERO, ASCII_ZERO, ASCII_ZERO,
            ASCII_ZERO, ASCII_ZERO, ASCII_ZERO,

            // JobStatus.Name
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,

            ETX,
        };

        static readonly IPEndPoint printEP = new IPEndPoint(IPAddress.Loopback, 9100);
        static readonly TcpListener listener = new TcpListener(printEP) { ExclusiveAddressUse = true };

        const double inch2mm = 25.4;
        const double dpi = 203;
        const double dot2mm = inch2mm / dpi; // 25.4(mm) / 203(dot) -> 0.125(mm/dot)
        const double mm2dot = 1 / dot2mm; // 8(dot/mm)

        [TestInitialize]
        public void Listen()
        {
            listener.Server.NoDelay = true;
            listener.Start(1);
        }

        [TestCleanup]
        public void Stop()
        {
            listener.Stop();
        }

        async static Task<byte[]> ResponseForPrint()
        {
            var buffers = new List<byte[]>();

            using (var client = await listener.AcceptTcpClientAsync())
            using (var stream = client.GetStream())
            {
                while (true)
                {
                    var dummy = new byte[client.ReceiveBufferSize];
                    var actual_buffer_length = await stream.ReadAsync(dummy, 0, dummy.Length);
                    if (actual_buffer_length == 0) break;

                    var buffer = dummy.Take(actual_buffer_length);
                    if (buffer.SequenceEqual(new byte[] { ENQ }))
                        await stream.WriteAsync(HealthOKBody, 0, HealthOKBody.Length);

                    buffers.Add(buffer.ToArray());
                }
            }

            return buffers.SelectMany(buffer => buffer).ToArray();
        }

        [TestMethod]
        public async Task MultiLabel()
        {
            var task = ResponseForPrint();
            int sent1 = 0, sent2 = 0, sent3 = 0;

            using (var printer = new Printer(printEP))
            {
                // page 1
                printer.Barcode.AddCODE128(1, 2, "HELLO");
                printer.SetPageNumber(3);
                sent1 = printer.AddStream();

                // page 2
                printer.Barcode.AddCODE128(4, 5, "WORLD");
                printer.SetPageNumber(6);
                sent2 = printer.AddStream();

                // page 3
                printer.Barcode.AddCODE128(7, 8, "!!!");
                printer.SetPageNumber(9);
                sent3 = printer.Send();
            }

            // page 1
            var expected1 = new string[]
            {
                "A",
                "BG" + "01" + "002" + "HELLO",
                "Q" + "000003",
                "Z",
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x)));

            // page 2
            var expected2 = new string[]
            {
                "A",
                "BG" + "04" + "005" + "WORLD",
                "Q" + "000006",
                "Z",
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x)));

            // page 3
            var expected3 = new string[]
            {
                "A",
                "BG" + "07" + "008" + "!!!",
                "Q" + "000009",
                "Z",
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x)));

            var expected = new byte[] { ENQ, STX }.Concat(expected1)
                .Concat(expected2)
                .Concat(expected3)
                .Append(ETX)
                .ToList();
            using (task)
            {
                Assert.AreEqual(expected.Count, sent1 + sent2 + sent3 + 1 /* ENQ count */);

                var actual = await task;
                CollectionAssert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public async Task ExampleBarcode()
        {
            var barcode = "1234567890128";
            var task = ResponseForPrint();
            int sent = 0;

            using (var printer = new Printer(printEP))
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
                sent = printer.Send();
            }

            var expected = (new byte[] { ENQ, STX }).Concat(new string[]
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
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x))))
            .Append(ETX).ToList();

            using (task)
            {
                Assert.AreEqual(expected.Count, sent + 1 /* ENQ count */);

                var actual = await task;
                CollectionAssert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public async Task ExampleGraphic()
        {
            var task = ResponseForPrint();
            int sent = 0;

            using (var printer = new Printer(printEP))
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
                sent = printer.Send();
            }

            var expected = (new byte[] { ENQ, STX }).Concat(new string[]
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
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x))))
            .Concat(File.ReadAllBytes("output.bmp"))
            // print
            .Concat((new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes("Q000001")))
            .Concat(new byte[] { ESC, ASCII_Z, ETX })
            .ToList();

            using (task)
            {
                Assert.AreEqual(expected.Count, sent + 1 /* ENQ count */);

                var actual = await task;
                CollectionAssert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public async Task AddCODE128()
        {
            var task = ResponseForPrint();
            int bar_width = 1, symbol_height = 2, X = 3, Y = 4;
            // start B + data1 + shift C + data2 + check + stop
            var print_data = "AaBbCc123456789";
            var symbol_width = 11 + 11 * 7 + 11 + 11 * 8 / 2 + 11 + 13;
            var operation = new byte[] { ESC }.Concat(Encoding.ASCII.GetBytes(
                "BG" + $"{bar_width:D2}" + $"{symbol_height:D3}"
                + ">HAaBbCc1>C23456789"));

            using (var printer = new Printer(printEP))
            {
                var size_codeBC = printer.Barcode.AddCODE128(bar_width, symbol_height, print_data,
                    (Size size) =>
                    {
                        Assert.AreEqual(symbol_width, size.Width);
                        Assert.AreEqual(symbol_height, size.Height);

                        printer.MoveToX(X);
                        printer.MoveToY(Y);
                    });
                Assert.AreEqual(symbol_width, size_codeBC.Width);
                Assert.AreEqual(symbol_height, size_codeBC.Height);

                var dummy = Encoding.ASCII.GetBytes("dummy");
                printer.PushOperation(dummy);
                CollectionAssert.AreEqual(dummy, printer.PopOperation());

                // start B + data + check + stop
                var symbol_width_codeB = 11 + 11 * 8 + 11 + 13;
                var size_codeB = printer.Barcode.AddCODE128(bar_width, symbol_height,
                    "ABC12345",
                    (Size size) =>
                    {
                        Assert.AreEqual(symbol_width_codeB, size.Width);
                        Assert.AreEqual(symbol_height, size.Height);
                    });
                Assert.AreEqual(symbol_width_codeB, size_codeB.Width);
                Assert.AreEqual(symbol_height, size_codeB.Height);
                printer.PopOperation();

                try
                {
                    // Explicitly passing the start code throws an exception.
                    _ = printer.Barcode.AddCODE128(1, 1, ">G", (Size _) => { });
                    Assert.Fail();
                }
                catch (Exception e)
                {
                    Assert.IsInstanceOfType(e, typeof(TinySatoArgumentException));
                }

                printer.Send();
            }

            var expected = (new byte[] { ENQ, STX }).Concat(new string[]
            {
                "A",
                $"H{X:D4}",
                $"V{Y:D4}",
            }.SelectMany(x => (new byte[] { ESC }).Concat(Encoding.ASCII.GetBytes(x))))
                .Concat(operation)
                .Concat(new byte[] { ESC, ASCII_Z, ETX });

            using (task)
            {
                var actual = await task;
                CollectionAssert.AreEqual(expected.ToArray(), actual);
            }
        }
    }
}
