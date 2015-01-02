using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CoolSerializer
{
    interface IFieldProvider
    {
        FieldInformation GetFieldInformation(TypeInformation parent, string name, FieldType fieldType);
        IEnumerable<FieldInformation> ProvideMembers(Type type);
    }

    internal abstract class FieldInformation
    {
        public string Name { get; protected set; }
        public FieldType RawType { get; protected set; }
        public Type Type { get; protected set; }
        public abstract Expression GetGetter(Expression paramter);
        public abstract Expression GetSetter(Expression paramter);
    }

    class FieldProvider : IFieldProvider
    {
        private const BindingFlags ReadWritePropertyFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty |
                                                            BindingFlags.SetProperty;

        public FieldInformation GetFieldInformation(TypeInformation parent, string name, FieldType fieldType)
        {
            //TODO: Add validation
            var info = parent.Type.GetProperty(name, ReadWritePropertyFlags);
            if (GetRawType(info.PropertyType) != fieldType)
            {
                throw new ArrayTypeMismatchException(string.Format("Field type should be {0} but instead is {1}", fieldType, info.PropertyType));
            }
            Func<Expression, Expression> getterCreator = x => Expression.MakeMemberAccess(x, info);
            Func<Expression, Expression> setterCreator = x => { throw new NotImplementedException(); };
            return new FieldInformationImpl(name, fieldType, info.PropertyType, getterCreator, setterCreator);
        }

        public IEnumerable<FieldInformation> ProvideMembers(Type type)
        {
            return
                type.GetProperties(ReadWritePropertyFlags).Select(info =>
                {
                    Func<Expression, Expression> getterCreator = x => Expression.MakeMemberAccess(x, info);
                    Func<Expression, Expression> setterCreator = x => { throw new NotImplementedException(); };
                    return new FieldInformationImpl(info.Name, GetRawType(info.PropertyType), info.PropertyType, getterCreator, setterCreator);
                });
        }

        public FieldType GetRawType(Type propertyType)
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

    internal class FieldInformationImpl : FieldInformation
    {
        private readonly Func<Expression, Expression> mGetter;
        private readonly Func<Expression, Expression> mSetter;

        public FieldInformationImpl(string name, FieldType rawType, Type type, Func<Expression, Expression> getterCreator, Func<Expression, Expression> setterCreator)
        {
            Name = name;
            RawType = rawType;
            Type = type;
            mGetter = getterCreator;
            mSetter = setterCreator;
        }
        public override Expression GetGetter(Expression paramter)
        {
            return mGetter(paramter);
        }

        public override Expression GetSetter(Expression paramter)
        {
            return mSetter(paramter);
        }
    }
}