using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CoolSerializer.V2
{
    class GuidMarshaler
    {
        private readonly byte[] mTempGuid;

        static GuidMarshaler()
        {
            if (!typeof(Guid).IsValueType)
            {
                throw new Exception("Fuck the world! Guid has become a reference type");
            }
        }

        public GuidMarshaler()
        {
            mTempGuid = new byte[Marshal.SizeOf(typeof(Guid))];
        }

        public void SerializeGuid(Stream stream, Guid guid)
        {
            unsafe
            {
                Guid* gptr = &guid;
                Marshal.Copy((IntPtr)gptr, mTempGuid, 0, mTempGuid.Length);
                stream.Write(mTempGuid, 0, mTempGuid.Length);
            }
        }

        public Guid DeserializeGuid(Stream stream)
        {
            Guid guid;
            unsafe
            {
                stream.Read(mTempGuid, 0, mTempGuid.Length);
                Guid* gptr = &guid;
                Marshal.Copy(mTempGuid, 0, (IntPtr)gptr, mTempGuid.Length);
            }
            return guid;
        }
    }
}