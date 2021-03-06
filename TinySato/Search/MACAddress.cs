﻿namespace TinySato.Search
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Net.NetworkInformation;

    public static class MACAddress
    {
        [SuppressMessage("Style", "CA1031")]
        public static bool TryParse(string mac_address, out PhysicalAddress mac)
        {
            mac = PhysicalAddress.None;

            try
            {
                var eui48address = mac_address.Replace(':', '-').ToUpper();
                mac = PhysicalAddress.Parse(eui48address);
                if (PhysicalAddress.None.Equals(mac))
                    return false;
            }
            catch (FormatException)
            {
                return false;
            }

            return true;
        }
    }
}
