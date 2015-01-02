namespace CoolSerializer
{
    class Consts
    {
        public const byte ComplexType = 1;
        public const byte StringType = 2;
        public const byte Number = 3;
        public const byte UnsignedNumber = 4;
        public const byte Guid = 5;


        public const byte ValueWire = 0 << 7;
        public const byte ReferenceWire = 1 << 7;
    }

    enum ComplexHeader : byte
    {
        Null = 0,
        Value = 1 << 7,
        Reference = 1 << 6,
        Boxing = (1 << 7) + (1 << 6)
    }

    enum FieldType : byte
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
}