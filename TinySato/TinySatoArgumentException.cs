namespace TinySato
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class TinySatoArgumentException : TinySatoException
    {
        public TinySatoArgumentException() { }

        public TinySatoArgumentException(string message)
            : base(message) { }

        public TinySatoArgumentException(string message, Exception inner)
            : base(message, inner) { }

        protected TinySatoArgumentException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
