using System;
using System.Collections.Generic;
using System.IO;

namespace CoolSerializer.V2
{
    class Document
    {
        private Stream mStream;
        private HashSet<Guid> mSentGuids;
        private BinaryWriter mBinaryWriter;
        public Document(Stream stream)
        {
            mStream = stream;
            mBinaryWriter = new BinaryWriter(mStream);
            mSentGuids = new HashSet<Guid>();
            GuidMarshaler = new GuidMarshaler();
        }

        public GuidMarshaler GuidMarshaler { get; private set; }

        public void WriteBytes(byte[] bytes)
        {
            
        }

        //public void WriteHeader(TypeInformation info)
        //{
        //    if (mSentGuids.Contains(info.TypeId))
        //    {
        //        GuidMarshaler.SerializeGuid(mStream, info.TypeId);
        //    }
        //    else
        //    {
        //        var serializedTypeInfo = info.GetSerializedTypeInfo();
        //        mStream.Write(serializedTypeInfo, 0, serializedTypeInfo.Length);
        //        mSentGuids.Add(info.TypeId);
        //    }
        //}

        public void WriteNumber(int number)
        {
            mBinaryWriter.Write(number);
        }

        public void WriteString(string str)
        {
            mBinaryWriter.Write(str);
        }

        public void WriteNull()
        {
            mBinaryWriter.Write((byte) ComplexHeader.Null);
        }
    }
}