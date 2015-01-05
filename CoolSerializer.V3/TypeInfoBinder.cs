using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace CoolSerializer.V3
{
    public interface IBoundTypeInfoFactory
    {
        IBoundTypeInfo Provide(TypeInfo info);
    }

    public interface IBoundFieldInfoProvider
    {
        IBoundFieldInfo[] Provide(TypeInfo info, Type realType);
    }

    class BoundFieldInfoProvider : IBoundFieldInfoProvider
    {
        public IBoundFieldInfo[] Provide(TypeInfo info, Type realType)
        {
            var fields = new IBoundFieldInfo[info.Fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = CreateBoundFieldInfo(realType, info.Fields[i]);
            }
            return fields;
        }

        private IBoundFieldInfo CreateBoundFieldInfo(Type objectType, FieldInfo fieldInfo)
        {
            return new BoundFieldInfo(objectType, fieldInfo);
        }
    }

    public class TypeInfoBinder : IBoundTypeInfoFactory
    {
        private readonly List<IBoundTypeInfoProvider> mProviders;
        public TypeInfoBinder(ISimplifersProvider simplifersProvider)
        {
            var fieldsProvider = new BoundFieldInfoProvider();
            mProviders = new List<IBoundTypeInfoProvider>
            {
                new CollecionBoundTypeInfoProvider(),
                new SimplifiedBoundTypeInfoProvider(simplifersProvider,fieldsProvider),
                new BasicBoundTypeInfoProvider(fieldsProvider)
            };
        }
        public IBoundTypeInfo Provide(TypeInfo info)
        {
            IBoundTypeInfo boundInfo;
            foreach (var provider in mProviders)
            {
                if (provider.TryProvide(info, out boundInfo))
                {
                    return boundInfo;
                }
            }

            throw new NotImplementedException("Unk types are not implemented atm");
        }
    }
    public interface IBoundTypeInfo
    {
        Type RealType { get; }
        TypeInfo TypeInfo { get; }
        Expression GetSerializeExpression(Expression graphParam, Expression writerParam, Serializer serializer);
        Expression GetDeserializeExpression(Expression readerParam, Deserializer deserializer);
    }
    public interface IBoundFieldInfo
    {
        Type RealType { get; }
        FieldType RawType { get; }
        Expression GetGetExpression(Expression graphParam, params Expression[] indexerParameters);
        Expression GetSetExpression(Expression graphParam, Expression graphFieldValue, params Expression[] indexerParameters);
    }
    public interface IBoundTypeInfoProvider
    {
        bool TryProvide(TypeInfo info, out IBoundTypeInfo boundTypeInfo);
    }
}