namespace TinySato
{
    using Search;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;

    partial class Printer
    {
        private static readonly object lockForSearch = new object();

        const byte ASCII_L = 0x4c, ASCII_A = 0x41;
        static readonly byte[] SearchRequestBody = new byte[] { ASCII_SOH, ASCII_L, ASCII_A };
        static readonly TimeSpan SearchWaitTimeout = TimeSpan.FromSeconds(3);
        const int DEFAULT_SEARCH_PORT = 19541;
        const int DEFAULT_PRINT_PORT = 9100;

        private static Dictionary<PhysicalAddress, IPAddress> cache
            = new Dictionary<PhysicalAddress, IPAddress>();

        public static void ClearSearchCache()
        {
            lock (lockForSearch)
            {
                cache.Clear();
            }
        }

        public static IEnumerable<Response> Search() { return Search(SearchWaitTimeout); }

        public static IEnumerable<Response> Search(TimeSpan wait_time, int request_port = DEFAULT_SEARCH_PORT)
        {
            lock (lockForSearch)
            {
                cache.Clear();
                var responses = new List<Response>();
                var recieve_port = Request(request_port);
                var timer = Stopwatch.StartNew();

                using (var server = new UdpClient(new IPEndPoint(IPAddress.Any, recieve_port)))
                {
                    server.EnableBroadcast = true;
                    for (var rest = wait_time; rest > TimeSpan.Zero; rest = wait_time - timer.Elapsed)
                    {
                        var task = server.ReceiveAsync();
                        if (!task.Wait(rest)) break;
                        Response response = null;
                        if (!Response.TryParse(task.Result.Buffer, ref response)) continue;
                        cache[response.MACAddress] = response.IPAddress;
                        responses.Add(response);
                    }
                }

                return responses.GroupBy(r => r.MACAddress.ToString()).Select(group => group.First());
            }
        }

        public static Printer Find(string mac_address)
        {
            if (!MACAddress.TryParse(mac_address, out PhysicalAddress mac))
            {
                throw new TinySatoException($"Bad physical address. address: {mac_address}");
            }

            return Find(mac, SearchWaitTimeout);
        }

        public static Printer Find(PhysicalAddress mac, TimeSpan wait_time, int request_port = DEFAULT_PRINT_PORT)
        {
            lock (lockForSearch)
            {
                if (cache.ContainsKey(mac)) { return new Printer(new IPEndPoint(cache[mac], request_port)); }

                var recieve_port = Request();
                using (var server = new UdpClient(new IPEndPoint(IPAddress.Any, recieve_port)))
                {
                    server.EnableBroadcast = true;
                    var timer = Stopwatch.StartNew();
                    for (var rest = wait_time; rest > TimeSpan.Zero; rest = wait_time - timer.Elapsed)
                    {
                        var task = server.ReceiveAsync();
                        if (!task.Wait(rest)) break;
                        Response response = null;
                        if (!Response.TryParse(task.Result.Buffer, ref response)) continue;
                        if (response.MACAddress.Equals(mac))
                        {
                            cache[response.MACAddress] = response.IPAddress;

                            return new Printer(new IPEndPoint(response.IPAddress, request_port));
                        }
                    }
                }
            }

            throw new TinySatoException("Not found printer. mac: " + mac);
        }

        protected static int Request(int request_port = DEFAULT_SEARCH_PORT)
        {
            using (var client = new UdpClient())
            {
                client.EnableBroadcast = true;
                client.Connect(new IPEndPoint(IPAddress.Broadcast, request_port));
                client.Send(SearchRequestBody, SearchRequestBody.Length);
                return ((IPEndPoint)client.Client.LocalEndPoint).Port;
            }
        }
    }
}
