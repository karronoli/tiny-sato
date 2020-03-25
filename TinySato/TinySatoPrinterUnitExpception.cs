namespace TinySato
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class TinySatoPrinterUnitException : TinySatoException
    {
        public TinySatoPrinterUnitException() { }

        public TinySatoPrinterUnitException(string message)
            : base(message) { }

        public TinySatoPrinterUnitException(string message, Exception inner)
            : base(message, inner) { }

        protected TinySatoPrinterUnitException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
