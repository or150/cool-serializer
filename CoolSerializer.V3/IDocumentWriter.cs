using System;

namespace CoolSerializer.V3
{
	public interface IDocumentWriter
	{
		void WriteBoolean(Boolean b);
		void WriteChar(Char c);
		void WriteSByte(SByte s);
		void WriteByte(Byte b);
		void WriteInt16(Int16 i);
		void WriteUInt16(UInt16 u);
		void WriteInt32(Int32 i);
		void WriteUInt32(UInt32 u);
		void WriteInt64(Int64 i);
		void WriteUInt64(UInt64 u);
		void WriteSingle(Single s);
		void WriteDouble(Double d);
		void WriteDecimal(Decimal d);
		void WriteDateTime(DateTime d);
		void WriteGuid(Guid g);
		void WriteString(String s);
		void WriteTypeInfo(TypeInfo i);
	}
}