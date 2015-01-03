using System;
using System.Text;

namespace CoolSerializer.V3
{
    public class TypeInfo
    {
        public TypeInfo(Guid guid, string name, FieldInfo[] fields, bool isAlwaysByVal)
        {
            Guid = guid;
            Name = name;
            Fields = fields;
            IsAlwaysByVal = isAlwaysByVal;
        }

        public Guid Guid { get; private set; }
        public string Name { get; private set; }
        public FieldInfo[] Fields { get; private set; }
        public bool IsAlwaysByVal { get; private set; }
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendFormat("{0} ({1})", Name, Guid);
            builder.AppendLine();
            builder.AppendLine("{");
            foreach (var fieldInfo in Fields)
            {
                builder.Append("\t");
                builder.AppendLine(fieldInfo.ToString());
            }
            builder.Append("}");
            return builder.ToString();
        }
    }
    public class FieldInfo
    {
        public FieldInfo(FieldType type, string name)
        {
            Type = type;
            Name = name;
        }

        public FieldType Type { get; private set; }
        public string Name { get; private set; }
        public override string ToString()
        {
            return string.Format("{0} {1}", Type.ToString(), Name.ToString());
        }
    }


    public enum FieldType : byte
    {
        Object,
        Boolean,
        Char,
        SByte,
        Byte,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Single,
        Double,
        Decimal,
        DateTime,
        Guid,
        String
    }

    [Flags]
    enum ComplexHeader : byte
    {
        Null = 0,
        Value = 1 << 0,//1 << 7,
        Reference = 1 << 1,//1 << 6,
        Boxing = Value + Reference
    }
}