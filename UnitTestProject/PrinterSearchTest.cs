namespace UnitTestProject
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using TinySato;
    using TinySato.Search;

    [TestClass]
    public class PrinterSearchTest
    {
        const string printer_mac = "02:00:00:00:00:01";
        static readonly string printer_mac_eui48 = printer_mac.Replace(':', '-').ToUpper();

        static readonly PhysicalAddress printer_physical_address = PhysicalAddress.Parse(printer_mac_eui48);
        static readonly IPEndPoint printEP = new IPEndPoint(IPAddress.Loopback, 9100);
        static readonly IPAddress subnet = IPAddress.Parse("255.0.0.0");
        static readonly IPAddress gateway = IPAddress.Parse("0.0.0.0");
        const string printer_name = "Lesprit Series";
        static readonly byte printer_dhcp = Convert.ToByte(true);
        static readonly byte printer_rarp = Convert.ToByte(true);

        static readonly IPEndPoint searchEP = new IPEndPoint(IPAddress.Any, 19541);

        const byte NULL = 0x0, SOH = 0x01, STX = 0x02, ETX = 0x03;
        const byte ASCII_COMMA = 0x2c, ASCII_A = 0x41, ASCII_L = 0x4c;

        readonly byte[] SearchResponseBody = new List<byte[]>
        {
            new byte[] { STX },
            printer_physical_address.GetAddressBytes(),
            new byte[] { ASCII_COMMA },
            printEP.Address.GetAddressBytes(),
            new byte[] { ASCII_COMMA },
            subnet.GetAddressBytes(),
            new byte[] { ASCII_COMMA },
            gateway.GetAddressBytes(),
            new byte[] { ASCII_COMMA },
            Encoding.ASCII.GetBytes(printer_name.PadRight(32, Convert.ToChar(NULL))),
            new byte[] { ASCII_COMMA },
            new byte[] { printer_dhcp },
            new byte[] { printer_rarp },
            new byte[] { ETX },
        }.SelectMany(x => x).ToArray();

        async Task<byte[]> ResponseForSearch()
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
            Response response = null;
            using (var task = ResponseForSearch())
            {
                var wait_time = TimeSpan.FromMilliseconds(500);
                var responses = Printer.Search(wait_time)
                    .Where(r => r.MACAddress.Equals(printer_physical_address));
                var health_request = await task;
                CollectionAssert.AreEqual(new byte[] { SOH, ASCII_L, ASCII_A }, health_request);
                Assert.IsInstanceOfType(responses, typeof(IEnumerable<Response>));
                Assert.AreEqual(1, responses.Count());
                response = responses.First();
            }

            Assert.AreEqual(printEP.Address, response.IPAddress);
            Assert.AreEqual(subnet, response.SubnetMask);
            Assert.AreEqual(gateway, response.Gateway);
            Assert.AreEqual(printer_name, response.Name);
            Assert.IsTrue(response.DHCP);
            Assert.IsTrue(response.RARP);
        }

        [TestMethod]
        [ExpectedException(typeof(TinySatoPrinterNotFoundException))]
        public void BusyPrinter()
        {
            using (var task = ResponseForSearch())
            {
                Printer.ClearSearchCache();
                using (var printer = Printer.Find(printer_mac)) { }
            }
        }
    }
}
