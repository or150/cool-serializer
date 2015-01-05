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

    public class TypeInfoBinder : IBoundTypeInfoFactory
    {
        private readonly List<IBoundTypeInfoProvider> mProviders;
        public TypeInfoBinder(ISimplifersProvider simplifersProvider)
        {
            mProviders = new List<IBoundTypeInfoProvider>
            {
                new CollecionBoundTypeInfoProvider(),
                new SimplifiedBoundTypeInfoProvider(simplifersProvider),
                new BasicBoundTypeInfoProvider()
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