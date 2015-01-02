using System;

namespace CoolSerializer.V3
{
    internal static class TypeExtenstions
    {
        internal static FieldType GetRawType(this Type propertyType)
        {
            switch (Type.GetTypeCode(propertyType))
            {
                case TypeCode.Object:
                {
                    if (propertyType == typeof(Guid))
                    {
                        return FieldType.Guid;
                    }
                    else
                    {
                        return FieldType.Object;
                    }
                }
                case TypeCode.Boolean:
                    return FieldType.Boolean;
                case TypeCode.Char:
                    return FieldType.Char;
                case TypeCode.SByte:
                    return FieldType.SByte;
                case TypeCode.Byte:
                    return FieldType.Byte;
                case TypeCode.Int16:
                    return FieldType.Int16;
                case TypeCode.UInt16:
                    return FieldType.UInt16;
                case TypeCode.Int32:
                    return FieldType.Int32;
                case TypeCode.UInt32:
                    return FieldType.UInt32;
                case TypeCode.Int64:
                    return FieldType.Int64;
                case TypeCode.UInt64:
                    return FieldType.UInt64;
                case TypeCode.Single:
                    return FieldType.Single;
                case TypeCode.Double:
                    return FieldType.Double;
                case TypeCode.Decimal:
                    return FieldType.Decimal;
                case TypeCode.DateTime:
                    return FieldType.DateTime;
                case TypeCode.String:
                    return FieldType.String;
                case TypeCode.Empty:
                    throw new NullReferenceException();
                case TypeCode.DBNull:
                    throw new NotSupportedException("DBNull is not supported");
                default:
                    throw new NotImplementedException();
            }
        }
    }
}