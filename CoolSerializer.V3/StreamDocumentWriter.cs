using System;
using System.Collections.Generic;
using System.IO;

namespace CoolSerializer.V3
{
    class StreamDocumentWriter : IDocumentWriter
    {
        private readonly Stream mStream;
        private GuidMarshaler mGuidWriter;
        private BinaryWriter mBinaryWriter;
        private HashSet<TypeInfo> mInfos = new HashSet<TypeInfo>(TypeInfoEqualityComparer.Instance);

        public StreamDocumentWriter(Stream stream)
        {
            mStream = stream;
            mGuidWriter = new GuidMarshaler();
            mBinaryWriter = new BinaryWriter(stream);
        }

        public void WriteBoolean(bool b)
        {
            mBinaryWriter.Write(b);
        }

        public void WriteChar(char c)
        {
            mBinaryWriter.Write(c);
        }

        public void WriteSByte(sbyte s)
        {
            mBinaryWriter.Write(s);
        }

        public void WriteByte(byte b)
        {
            mBinaryWriter.Write(b);
        }

        public void WriteInt16(short i)
        {
            mBinaryWriter.Write(i);
        }

        public void WriteUInt16(ushort u)
        {
            mBinaryWriter.Write(u);
        }

        public void WriteInt32(int i)
        {
            mBinaryWriter.Write(i);
        }

        public void WriteUInt32(uint u)
        {
            mBinaryWriter.Write(u);
        }

        public void WriteInt64(long i)
        {
            mBinaryWriter.Write(i);
        }

        public void WriteUInt64(ulong u)
        {
            mBinaryWriter.Write(u);
        }

        public void WriteSingle(float s)
        {
            mBinaryWriter.Write(s);
        }

        public void WriteDouble(double d)
        {
            mBinaryWriter.Write(d);
        }

        public void WriteDecimal(decimal d)
        {
            mBinaryWriter.Write(d);
        }

        public void WriteDateTime(DateTime d)
        {
            mBinaryWriter.Write(d.ToString("O"));
        }

        public void WriteGuid(Guid g)
        {
            mGuidWriter.SerializeGuid(mStream,g);
        }

        public void WriteString(string s)
        {
            mBinaryWriter.Write(s);
        }

        public void WriteTypeInfo(TypeInfo i)
        {
            if (mInfos.Add(i))
            {
                mGuidWriter.SerializeGuid(mStream, i.Guid);
                mBinaryWriter.Write(i.Name);
                mBinaryWriter.Write((byte) i.RawType);
                mBinaryWriter.Write(i.IsAlwaysByVal);
                mBinaryWriter.Write(i.Fields.Length);
                foreach (var fieldInfo in i.Fields)
                {
                    mBinaryWriter.Write((byte) fieldInfo.Type);
                    mBinaryWriter.Write(fieldInfo.Name);
                }
            }
            else
            {
                WriteGuid(i.Guid);
            }
        }
    }
}