using System;
using System.Linq.Expressions;
using System.Reflection;

namespace CoolSerializer.V3
{
    public class TypeInfoAssemblyBinder
    {
        public IBoundTypeInfo Provide(TypeInfo info)
        {
            var realType = Type.GetType(info.Name);
            if (realType == null)
            {
                throw new NotImplementedException("Unk types are not implemented atm");
            }
            var fields = new IBoundFieldInfo[info.Fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = CreateBoundedFieldInfo(realType, info.Fields[i]);
            }
            return new BoundTypeInfo(info,realType,fields);
        }

        private IBoundFieldInfo CreateBoundedFieldInfo(Type objectType, FieldInfo fieldInfo)
        {
            if (fieldInfo.Type == FieldType.ObjectByVal)
            {
                return new ByValBoundFieldInfo(objectType,fieldInfo,this);
            }
            return new BoundFieldInfo(objectType,fieldInfo);
        }
    }

    public interface IBoundTypeInfo
    {
        Type RealType { get; }
        TypeInfo TypeInfo { get; }
        IBoundFieldInfo[] Fields { get; }
    }

    public class BoundTypeInfo : IBoundTypeInfo
    {
        public BoundTypeInfo(TypeInfo typeInfo, Type realType, IBoundFieldInfo[] fields)
        {
            RealType = realType;
            TypeInfo = typeInfo;
            Fields = fields;
        }

        public Type RealType { get; private set; }
        public TypeInfo TypeInfo { get; private set; }
        public IBoundFieldInfo[] Fields { get; private set; }
    }

    public interface IBoundFieldInfo
    {
        Type RealType { get; }
        FieldInfo FieldInfo { get; }
        Expression GetGetExpression(Expression graphParam);
        Expression GetSetExpression(Expression graphParam, Expression graphFieldValue);
    }

    public interface IByValBoundFieldInfo : IBoundFieldInfo
    {
        IBoundTypeInfo TypeInfo { get; }
    }

    public class BoundFieldInfo : IBoundFieldInfo
    {
        private readonly PropertyInfo mInfo;

        public BoundFieldInfo(Type objectType, FieldInfo fieldInfo)
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

    public class ByValBoundFieldInfo : BoundFieldInfo, IByValBoundFieldInfo
    {
        public ByValBoundFieldInfo(Type objectType, FieldInfo fieldInfo, TypeInfoAssemblyBinder typeInfoAssemblyBinder)
            : base(objectType, fieldInfo)
        {
            TypeInfo = typeInfoAssemblyBinder.Provide(((ByValFieldInfo) fieldInfo).TypeInfo);
        }

        public IBoundTypeInfo TypeInfo { get; private set; }
    }
}