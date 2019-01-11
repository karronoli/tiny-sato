namespace TinySato
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class TinySatoException : Exception
    {
        public TinySatoException() { }

        public TinySatoException(string message)
            : base(message) { }

        public TinySatoException(string message, Exception inner)
            : base(message, inner) { }

        protected TinySatoException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
