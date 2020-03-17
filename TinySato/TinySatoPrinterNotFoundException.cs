namespace TinySato
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class TinySatoPrinterNotFoundException : TinySatoException
    {
        public TinySatoPrinterNotFoundException() { }

        public TinySatoPrinterNotFoundException(string message)
            : base(message) { }

        public TinySatoPrinterNotFoundException(string message, Exception inner)
            : base(message, inner) { }

        protected TinySatoPrinterNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
