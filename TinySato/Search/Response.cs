namespace TinySato.Search
{
    using System;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Runtime.InteropServices;

    public class Response
    {
        public PhysicalAddress MACAddress { get; }

        public IPAddress IPAddress { get; }

        public IPAddress SubnetMask { get; }

        public IPAddress Gateway { get; }

        public string Name { get; }

        public bool DHCP { get; }

        public bool RARP { get; }

        public Response(byte[] raw)
        {
            var size = Marshal.SizeOf<RawResponse>();
            var ptr = Marshal.AllocCoTaskMem(size);
            Marshal.Copy(raw, 0, ptr, size);
            var response = Marshal.PtrToStructure<RawResponse>(ptr);
            Marshal.FreeCoTaskMem(ptr);

            if (response.STX != Printer.ASCII_STX || response.ETX != Printer.ASCII_ETX)
            {
                throw new NotImplementedException();
            }

            MACAddress = new PhysicalAddress(response.MACAddress);
            IPAddress = new IPAddress(response.IPAddress);
            SubnetMask = new IPAddress(response.SubnetMask);
            Gateway = new IPAddress(response.Gateway);
            Name = response.Name;
            DHCP = response.DHCP;
            RARP = response.RARP;
        }

        public static bool TryParse(byte[] raw, ref Response response)
        {
            try
            {
                response = new Response(raw);

                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RawResponse
    {
        [MarshalAs(UnmanagedType.U1)]
        internal byte STX;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        internal byte[] MACAddress;

        [MarshalAs(UnmanagedType.U1)]
        readonly byte Delimiter1;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        internal byte[] IPAddress;

        [MarshalAs(UnmanagedType.U1)]
        readonly byte Delimiter2;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        internal byte[] SubnetMask;

        [MarshalAs(UnmanagedType.U1)]
        readonly byte Delimiter3;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        internal byte[] Gateway;

        [MarshalAs(UnmanagedType.U1)]
        readonly byte Delimiter4;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string Name;

        [MarshalAs(UnmanagedType.U1)]
        readonly byte Delimiter5;

        [MarshalAs(UnmanagedType.I1)]
        internal bool DHCP;

        [MarshalAs(UnmanagedType.I1)]
        internal bool RARP;

        [MarshalAs(UnmanagedType.U1)]
        internal byte ETX;
    }
}
