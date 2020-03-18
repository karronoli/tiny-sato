namespace TinySato
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class TinySatoIOException : TinySatoException
    {
        public TinySatoIOException() { }

        public TinySatoIOException(string message)
            : base(message) { }

        public TinySatoIOException(string message, Exception inner)
            : base(message, inner) { }

        protected TinySatoIOException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
