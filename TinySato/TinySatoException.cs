using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinySato
{
    public class TinySatoException : Exception
    {
        public TinySatoException() { }

        public TinySatoException(string message)
            : base(message) { }

        public TinySatoException(string message, Exception inner)
            : base(message, inner) { }

    }
}
