namespace TinySato.Communication
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;

    public class JobStatus
    {
        public string ID { get; }

        public Health Health { get; }

        public int LabelRemaining { get; }

        public string Name { get; }

        static readonly byte[] RequestBody = new byte[] { Printer.ASCII_ENQ };

        static readonly TimeSpan IOWaitTimeout = TimeSpan.FromSeconds(10.0);

        // Don't dispose for persistent connection to printer
        protected Stream stream;

        public JobStatus(Stream stream)
        {
            this.stream = stream;

            RawJobStatus response;
            var writeTimeoutOrginal = stream.WriteTimeout;
            var readTimeoutOriginal = stream.ReadTimeout;

            try
            {
                // Enquire status
                stream.WriteTimeout = (int)IOWaitTimeout.TotalMilliseconds;
                stream.Write(RequestBody, 0, RequestBody.Length);
                // Return status
                var buffer = new byte[1024];
                stream.ReadTimeout = (int)IOWaitTimeout.TotalMilliseconds;
                var actual_length = stream.Read(buffer, 0, buffer.Length);
                // Marshal status
                var expected_length = Marshal.SizeOf<RawJobStatus>();
                var ptr = Marshal.AllocCoTaskMem(expected_length);
                // Skip Ethernet padding
                Marshal.Copy(buffer, actual_length - expected_length, ptr, expected_length);
                response = Marshal.PtrToStructure<RawJobStatus>(ptr);
                Marshal.FreeCoTaskMem(ptr);
            }
            catch (IOException e)
            {
                throw new TinySatoException("The printer is not respond.", e);
            }
            finally
            {
                stream.WriteTimeout = writeTimeoutOrginal;
                stream.ReadTimeout = readTimeoutOriginal;
            }

            if (response.STX != Printer.ASCII_STX || response.ETX != Printer.ASCII_ETX)
            {
                throw new NotImplementedException();
            }

            ID = response.ID;
            Health = new Health(response.Health);
            LabelRemaining = int.Parse(response.LabelRemaining);
            Name = response.Name;

            if (Health.Error != Error.None)
                throw new TinySatoException($"Printer failure. error: {Enum.GetName(typeof(Error), Health.Error)}");
        }

        public bool OK
        {
            get
            {
                return (Health.State == State.Online
                    || Health.State == State.OnlinePrinting
                    || Health.State == State.OnlineDispense
                    || Health.State == State.OnlineAnalyzing)
                    && Health.Battery == Battery.OK
                    && Health.Buffer == Buffer.OK;
            }
        }

        public JobStatus Refresh() => new JobStatus(stream);

        public override string ToString()
        {
            var message = $"{nameof(ID)}: {ID}";
            message += $", {nameof(Name)}: {Name}";
            message += $", {nameof(LabelRemaining)}: {LabelRemaining}";
            message += $", {nameof(Health.State)}: {Enum.GetName(typeof(State), Health.State)}";
            message += $", {nameof(Health.Battery)}: {Enum.GetName(typeof(Battery), Health.Battery)}";
            message += $", {nameof(Health.Buffer)}: {Enum.GetName(typeof(Buffer), Health.Buffer)}";
            message += $", {nameof(Health.Error)}: {Enum.GetName(typeof(Error), Health.Error)}";
            return message;
        }
    }

    public enum State
    {
        Offline,
        Online, OnlinePrinting, OnlineDispense, OnlineAnalyzing,
        Error
    }

    public enum Battery { NearEnd, OK, Unknown }

    public enum Buffer { NearFull, OK, Unknown }

    public enum Error { None, Buffer, Paper, Battery, Sensor, Head, CoverOpen, Other }

    public struct Health
    {
        public char Raw { get; }
        public State State { get; }
        public Battery Battery { get; }
        public Buffer Buffer { get; }
        public Error Error { get; }

        static readonly List<Health> Definition = new List<Health>()
        {
            new Health('0', State.Offline),
            new Health('1', State.Offline, Battery.NearEnd),
            new Health('2', State.Offline, Buffer.NearFull),
            new Health('3', State.Offline, Battery.NearEnd, Buffer.NearFull),

            new Health('A', State.Online),
            new Health('B', State.Online, Battery.NearEnd),
            new Health('C', State.Online, Buffer.NearFull),
            new Health('D', State.Online, Battery.NearEnd, Buffer.NearFull),

            new Health('G', State.OnlinePrinting),
            new Health('H', State.OnlinePrinting, Battery.NearEnd),
            new Health('I', State.OnlinePrinting, Buffer.NearFull),
            new Health('J', State.OnlinePrinting, Battery.NearEnd, Buffer.NearFull),

            new Health('M', State.OnlineDispense),
            new Health('N', State.OnlineDispense, Battery.NearEnd),
            new Health('O', State.OnlineDispense, Buffer.NearFull),
            new Health('P', State.OnlineDispense, Battery.NearEnd, Buffer.NearFull),

            new Health('S', State.OnlineAnalyzing),
            new Health('T', State.OnlineAnalyzing, Battery.NearEnd),
            new Health('U', State.OnlineAnalyzing, Buffer.NearFull),
            new Health('V', State.OnlineAnalyzing, Battery.NearEnd, Buffer.NearFull),

            new Health('a', Error.Buffer),
            new Health('c', Error.Paper),
            new Health('d', Error.Battery),
            new Health('f', Error.Sensor),
            new Health('g', Error.Head),
            new Health('h', Error.CoverOpen),
            new Health('k', Error.Other),
        };

        public Health(char status)
        {
            var health = Definition.Where(h => h.Raw == status);
            if (health.Count() != 1)
                throw new TinySatoException($"Printer status is unknown. status:{status}");
            this = health.First();
        }

        private Health(char raw, State state) : this(raw, state, Battery.OK, Buffer.OK) { }
        private Health(char raw, State state, Battery battery) : this(raw, state, battery, Buffer.OK) { }
        private Health(char raw, State state, Buffer buffer) : this(raw, state, Battery.OK, buffer) { }

        private Health(char raw, State state, Battery battery, Buffer buffer)
        {
            Raw = raw;
            State = state;
            Battery = battery;
            Buffer = buffer;
            Error = Error.None;
        }

        private Health(char raw, Error error)
        {
            Raw = raw;
            State = State.Error;
            Battery = Battery.Unknown;
            Buffer = Buffer.Unknown;
            Error = error;
        }

        public override int GetHashCode() => Raw;

        public override bool Equals(object obj)
        {
            if (!(obj is Health))
                return false;

            return Equals((Health)obj);
        }

        public bool Equals(Health other) => Raw == other.Raw;

        public static bool operator ==(Health left, Health right) => left.Equals(right);

        public static bool operator !=(Health left, Health right) => !left.Equals(right);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawJobStatus
    {
        [MarshalAs(UnmanagedType.U1)]
        internal byte STX;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2)]
        internal string ID;

        [MarshalAs(UnmanagedType.U1)]
        internal char Health;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 6)]
        internal string LabelRemaining;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        internal string Name;

        [MarshalAs(UnmanagedType.U1)]
        internal byte ETX;
    }
}
