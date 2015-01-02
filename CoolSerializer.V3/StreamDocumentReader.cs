using System;
using System.Collections.Generic;
using System.IO;

namespace CoolSerializer.V3
{
    class StreamDocumentReader : IDocumentReader
    {
        private readonly Stream mStream;
        private GuidMarshaler mGuidReader;
        private BinaryReader mBinaryReader;
        private Dictionary<Guid,TypeInfo> mInfos = new Dictionary<Guid, TypeInfo>();

        public StreamDocumentReader(Stream stream)
        {
            mStream = stream;
            mGuidReader = new GuidMarshaler();
            mBinaryReader = new BinaryReader(stream);
        }

        public Guid ReadGuid()
        {
            return mGuidReader.DeserializeGuid(mStream);
        }

        public string ReadString()
        {
            return mBinaryReader.ReadString();
        }

        public decimal ReadDecimal()
        {
            return mBinaryReader.ReadDecimal();
        }

        public DateTime ReadDateTime()
        {
            return DateTime.Parse(mBinaryReader.ReadString());
        }

        public float ReadSingle()
        {
            return mBinaryReader.ReadSingle();
        }

        public double ReadDouble()
        {
            return mBinaryReader.ReadDouble();
        }

        public ulong ReadUInt64()
        {
            return mBinaryReader.ReadUInt64();
        }

        public long ReadInt64()
        {
            return mBinaryReader.ReadInt64();
        }

        public uint ReadUInt32()
        {
            return mBinaryReader.ReadUInt32();
        }

        public int ReadInt32()
        {
            return mBinaryReader.ReadInt32();
        }

        public ushort ReadUInt16()
        {
            return mBinaryReader.ReadUInt16();
        }

        public short ReadInt16()
        {
            return mBinaryReader.ReadInt16();
        }

        public char ReadChar()
        {
            return mBinaryReader.ReadChar();
        }

        public sbyte ReadSByte()
        {
            return mBinaryReader.ReadSByte();
        }

        public byte ReadByte()
        {
            return mBinaryReader.ReadByte();
        }

        public bool ReadBoolean()
        {
            return mBinaryReader.ReadBoolean();
        }

        public TypeInfo ReadTypeInfo()
        {
            var guid = mGuidReader.DeserializeGuid(mStream);
            TypeInfo info;
            if (mInfos.TryGetValue(guid, out info))
            {
                return info;
            }

            var name = mBinaryReader.ReadString();
            var fieldsCount = ReadInt32();
            var fields = new FieldInfo[fieldsCount];
            for (int i = 0; i < fieldsCount; i++)
            {
                var type = (FieldType) mBinaryReader.ReadByte();
                var fieldName = mBinaryReader.ReadString();
                if (type == FieldType.ObjectByVal)
                {
                    var typeDefinition = ReadTypeInfo();
                    fields[i] = new ByValFieldInfo(typeDefinition,fieldName);
                }
                else
                {
                    fields[i] = new FieldInfo(type, fieldName);
                }
            }

            return new TypeInfo(guid,name,fields);
        }
    }
}