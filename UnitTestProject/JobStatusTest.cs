namespace UnitTestProject
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using TinySato;

    [TestClass]
    public class JobStatusTest
    {
        const byte NULL = 0x00, STX = 0x02, ETX = 0x03, ENQ = 0x05, ESC = 0x1b, FS = 0x1c;
        const byte ASCII_SPACE = 0x20, ASCII_ZERO = 0x30, ASCII_A = 0x41, ASCII_Z = 0x5a;

        // STATUS4 standard http://www.sato.co.jp/webmanual/printer/cl4nx-j_cl6nx-j/main/main_GUID-D94C3DAD-1A55-4706-A86D-71EF71C6F3E3.html#
        static readonly byte[] HealthOKBody = new byte[]
        {
            NULL, NULL, NULL, FS,
            ENQ,
            STX,

            // JobStatus.ID
            ASCII_SPACE, ASCII_SPACE,

            // JobStatus.Health
            Convert.ToByte('A'),

            // JobStatus.LabelRemaining
            ASCII_ZERO, ASCII_ZERO, ASCII_ZERO,
            ASCII_ZERO, ASCII_ZERO, ASCII_ZERO,

            // JobStatus.Name
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,

            ETX,
        };

        static readonly byte[] HealthOnlinePrintingBufferNearFullBody = new byte[]
        {
            NULL, NULL, NULL, FS,
            ENQ,
            STX,

            // JobStatus.ID
            ASCII_SPACE, ASCII_SPACE,

            // JobStatus.Health
            Convert.ToByte('C'),

            // JobStatus.LabelRemaining
            ASCII_ZERO, ASCII_ZERO, ASCII_ZERO,
            ASCII_ZERO, ASCII_ZERO, ASCII_ZERO,

            // JobStatus.Name
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,

            ETX,
        };

        static readonly byte[] HealthOfflineBody = new byte[]
        {
            NULL, NULL, NULL, FS,
            ENQ,
            STX,

            // JobStatus.ID
            ASCII_SPACE, ASCII_SPACE,

            // JobStatus.Health
            Convert.ToByte('0'),

            // JobStatus.LabelRemaining
            ASCII_ZERO, ASCII_ZERO, ASCII_ZERO,
            ASCII_ZERO, ASCII_ZERO, ASCII_ZERO,

            // JobStatus.Name
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,

            ETX,
        };

        static readonly byte[] HealthErrorPaperBody = new byte[]
        {
            NULL, NULL, NULL, FS,
            ENQ,
            STX,

            // JobStatus.ID
            ASCII_SPACE, ASCII_SPACE,

            // JobStatus.Health
            Convert.ToByte('c'),

            // JobStatus.LabelRemaining
            ASCII_ZERO, ASCII_ZERO, ASCII_ZERO,
            ASCII_ZERO, ASCII_ZERO, ASCII_ZERO,

            // JobStatus.Name
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,
            ASCII_SPACE, ASCII_SPACE, ASCII_SPACE, ASCII_SPACE,

            ETX,
        };

        static readonly byte[] HealthErrorHeadBody = new byte[]
        {
            NULL, NULL, NULL, FS,
            ENQ,
            STX,

            // JobStatus.ID
            ASCII_SPACE, ASCII_SPACE,

            // JobStatus.Health
            Convert.ToByte('g'),

            // JobStatus.LabelRemaining
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
        static TcpListener listener;

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

        static async Task<byte[]> ResponseForPrint(IEnumerable<byte[]> health_responses)
        {
            var buffers = new List<byte[]>();

            using (var client = await listener.AcceptTcpClientAsync())
            using (var stream = client.GetStream())
            {
                var i = 0;
                while (true)
                {
                    var dummy = new byte[client.ReceiveBufferSize];
                    var actual_buffer_length = await stream.ReadAsync(dummy, 0, dummy.Length);
                    var buffer = dummy.Take(actual_buffer_length).ToArray();

                    if (buffer.Length == 0)
                    {
                        var last = buffers.Last();
                        if (last.Last() == ETX) break;
                        Assert.Fail("bad request body");
                    }

                    if (buffer.Last() == ENQ)
                    {
                        var health = health_responses.ElementAt(i);
                        await stream.WriteAsync(health, 0, health.Length);
                        ++i;
                    }

                    buffers.Add(buffer);
                }
            }

            return buffers.SelectMany(buffer => buffer).ToArray();
        }

        [TestMethod]
        public async Task OnlineBufferNearFull()
        {
            var task = ResponseForPrint(new List<byte[]> { HealthOnlinePrintingBufferNearFullBody, HealthOKBody, HealthOKBody });
            using (var printer = new Printer(printEP))
            {
                printer.Send();
            }

            using (task)
            {
                var expected = new List<byte>
                {
                    ENQ, ENQ, ENQ,
                    STX,
                    ESC, ASCII_A,
                    ESC, ASCII_Z,
                    ETX,
                };

                var actual = await task;
                CollectionAssert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(TinySatoIOException))]
        public void ConnectTimeout()
        {
            var responses = Enumerable.Repeat(HealthOfflineBody, 1000);
            _ = ResponseForPrint(responses);

            using (var printer = new Printer(printEP)) { }
        }

        [TestMethod]
        [ExpectedException(typeof(TinySatoException))]
        public void HealthErrorHead()
        {
            _ = ResponseForPrint(new List<byte[]> { HealthErrorHeadBody });

            using (var printer = new Printer(printEP)) { }
        }

        [TestMethod]
        public async Task Offline()
        {
            var task = ResponseForPrint(new List<byte[]> { HealthOfflineBody, HealthOKBody, HealthOKBody });
            using (var printer = new Printer(printEP))
            {
                printer.Send();
            }

            using (task)
            {
                var expected = new List<byte>
                {
                    ENQ, ENQ, ENQ,
                    STX,
                    ESC, ASCII_A,
                    ESC, ASCII_Z,
                    ETX,
                };

                var actual = await task;
                CollectionAssert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(TinySatoException))]
        public void PaperError()
        {
            _ = ResponseForPrint(new List<byte[]> { HealthErrorPaperBody, HealthOKBody, HealthOKBody });

            using (var printer = new Printer(printEP)) { }
        }

        [TestMethod]
        [ExpectedException(typeof(TinySatoException))]
        public void NoWaitAtMultiLabel()
        {
            _ = ResponseForPrint(new List<byte[]> { HealthOKBody, HealthOKBody, HealthOnlinePrintingBufferNearFullBody });

            using (var printer = new Printer(printEP))
            {
                printer.AddStream();
                printer.Send();
            }
        }
    }
}
