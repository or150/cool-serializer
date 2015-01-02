using System;
using System.IO;

namespace CoolSerializer
{
    internal class ObjectDeserializer
    {
        public T Deserialize<T>(Stream ms)
        {
            return GetClimbDelegate<T>(new DeserializeDocument(ms));
        }

        private T GetClimbDelegate<T>(DeserializeDocument deserializeDocument)
        {
            throw new NotImplementedException();
        }
    }

    internal class DeserializeDocument
    {
        public DeserializeDocument(Stream ms)
        {
            throw new NotImplementedException();
        }
    }
}