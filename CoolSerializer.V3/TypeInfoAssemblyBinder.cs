using System;
using System.Linq.Expressions;
using System.Reflection;

namespace CoolSerializer.V3
{
    public class TypeInfoAssemblyBinder
    {
        public IBoundedTypeInfo Provide(TypeInfo info)
        {
            var realType = Type.GetType(info.Name);
            if (realType == null)
            {
                throw new NotImplementedException("Unk types are not implemented atm");
            }
            var fields = new IBoundedFieldInfo[info.Fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = CreateBoundedFieldInfo(realType, info.Fields[i]);
            }
            return new BoundedTypeInfo(info,realType,fields);
        }

        private IBoundedFieldInfo CreateBoundedFieldInfo(Type objectType, FieldInfo fieldInfo)
        {
            if (fieldInfo.Type == FieldType.ObjectByVal)
            {
                return new ByValBoundedFieldInfo(objectType,fieldInfo,this);
            }
            return new BoundedFieldInfo(objectType,fieldInfo);
        }
    }

    public interface IBoundedTypeInfo
    {
        Type RealType { get; }
        TypeInfo TypeInfo { get; }
        IBoundedFieldInfo[] Fields { get; }
    }

    public class BoundedTypeInfo : IBoundedTypeInfo
    {
        public BoundedTypeInfo(TypeInfo typeInfo, Type realType, IBoundedFieldInfo[] fields)
        {
            RealType = realType;
            TypeInfo = typeInfo;
            Fields = fields;
        }

        public Type RealType { get; private set; }
        public TypeInfo TypeInfo { get; private set; }
        public IBoundedFieldInfo[] Fields { get; private set; }
    }

    public interface IBoundedFieldInfo
    {
        Type RealType { get; }
        FieldInfo FieldInfo { get; }
        Expression GetGetExpression(Expression graphParam);
        Expression GetSetExpression(Expression graphParam, Expression graphFieldValue);
    }

    public interface IByValBoundedFieldInfo : IBoundedFieldInfo
    {
        IBoundedTypeInfo TypeInfo { get; }
    }

    public class BoundedFieldInfo : IBoundedFieldInfo
    {
        private readonly PropertyInfo mInfo;

        public BoundedFieldInfo(Type objectType, FieldInfo fieldInfo)
        {
            mInfo = objectType.GetProperty(fieldInfo.Name);
            if (mInfo == null)
            {
                throw new NotImplementedException();
            }
            
            RealType = mInfo.PropertyType;
            FieldInfo = fieldInfo;
        }

        public Type RealType { get; private set; }
        public FieldInfo FieldInfo { get; private set; }

        public Expression GetGetExpression(Expression graphParam)
        {
            return Expression.MakeMemberAccess(graphParam, mInfo);
        }

        public Expression GetSetExpression(Expression graphParam, Expression graphFieldValue)
        {
            return Expression.Assign(Expression.MakeMemberAccess(graphParam, mInfo), graphFieldValue);
        }
    }

    public class ByValBoundedFieldInfo : BoundedFieldInfo, IByValBoundedFieldInfo
    {
        public ByValBoundedFieldInfo(Type objectType, FieldInfo fieldInfo, TypeInfoAssemblyBinder typeInfoAssemblyBinder)
            : base(objectType, fieldInfo)
        {
            TypeInfo = typeInfoAssemblyBinder.Provide(((ByValFieldInfo) fieldInfo).TypeInfo);
        }

        public IBoundedTypeInfo TypeInfo { get; private set; }
    }
}