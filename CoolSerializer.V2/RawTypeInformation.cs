using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoolSerializer.V2
{
    internal sealed class RawTypeInformation
    {
        internal RawTypeInformation(string name, List<RawFieldInformation> fields)
        {
            Name = name;
            Fields = fields;
        }

        //public Guid Id { get; private set; }
        public string Name { get; private set; }
        public List<RawFieldInformation> Fields { get; private set; }

        public byte[] Serialize()
        {
            var memStream = new MemoryStream();
            memStream.Position = sizeof(int);
            using (var writer = new BinaryWriter(memStream,Encoding.UTF8,true))
            {
                writer.Write(Name);
                writer.Write(Fields.Count);
                foreach (var field in Fields)
                {
                    field.WriteTo(writer);
                }
            }
            memStream.Position = 0;
            memStream.Write(BitConverter.GetBytes((int)(memStream.Length - sizeof(int))),0,sizeof(int));
            return memStream.ToArray();
        }

        public static RawTypeInformation Deserialize(Stream graphStream)
        {
            using(var reader = new BinaryReader(graphStream,Encoding.UTF8,true))
            {
                reader.ReadInt32();//Length
                var name = reader.ReadString();
                var fieldsCount = reader.ReadInt32();
                var fields = new List<RawFieldInformation>(fieldsCount);
                for (int i = 0; i < fieldsCount; i++)
                {
                    fields.Add(RawFieldInformation.Deserialize(reader));
                }
                return new RawTypeInformation(name,fields);
            }
        }

        public override string ToString()
        {
            return "RawTypeInfo:" + Name;
        }
    }

    internal class RawFieldInformation
    {
        public string Name { get; private set; }
        public FieldType RawType { get; private set; }

        internal RawFieldInformation(string name, FieldType type)
        {
            Name = name;
            RawType = type;
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write((byte)RawType);
        }

        public static RawFieldInformation Deserialize(BinaryReader reader)
        {
            var name = reader.ReadString();
            var type = (FieldType)reader.ReadByte();
            return new RawFieldInformation(name, type);
        }

        public override string ToString()
        {
            return "RawFieldInfo: {" + RawType + ", " + Name + "}";
        }
    }

}