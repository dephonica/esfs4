using System;

namespace EsfsLite
{
    class EsfsException : Exception
    {
        public EsfsException()
        {
            
        }

        public EsfsException(string message) : base(message)
        {
            
        }
    }
}
