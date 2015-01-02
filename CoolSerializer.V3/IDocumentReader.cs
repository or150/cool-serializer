using System;

namespace CoolSerializer.V3
{
	public interface IDocumentReader
	{
		Boolean ReadBoolean();
		Char ReadChar();
		SByte ReadSByte();
		Byte ReadByte();
		Int16 ReadInt16();
		UInt16 ReadUInt16();
		Int32 ReadInt32();
		UInt32 ReadUInt32();
		Int64 ReadInt64();
		UInt64 ReadUInt64();
		Single ReadSingle();
		Double ReadDouble();
		Decimal ReadDecimal();
		DateTime ReadDateTime();
		Guid ReadGuid();
		String ReadString();
		TypeInfo ReadTypeInfo();
	}
}