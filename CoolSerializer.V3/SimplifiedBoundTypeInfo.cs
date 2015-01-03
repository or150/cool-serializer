using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;

namespace CoolSerializer.V3
{
    public class SimplifiedBoundTypeInfo : BoundTypeInfo
    {
        private readonly object mSimplifier;

        public SimplifiedBoundTypeInfo(TypeInfo typeInfo, object simplifier)
            : base(typeInfo, GetRealType(simplifier.GetType()), GetFields(typeInfo, simplifier.GetType()))
        {
            mSimplifier = simplifier;
        }

        private static IBoundFieldInfo[] GetFields(TypeInfo typeInfo, Type simplifierType)
        {
            var type = GetSimplifiedType(simplifierType);
            var fields = new IBoundFieldInfo[typeInfo.Fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = new BoundFieldInfo(type,typeInfo.Fields[i]);
            }
            return fields;
        }

        private static Type GetRealType(Type simplifierType)
        {
            return GetSimplifierType(simplifierType).GetGenericArguments()[0];
        }
        private static Type GetSimplifiedType(Type simplifierType)
        {
            return GetSimplifierType(simplifierType).GetGenericArguments()[1];
        }

        private static Type GetSimplifierType(Type simplifierType)
        {
            return simplifierType.GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof (ISimpifier<,>));
        }

        protected override Expression GetFieldsSerializeExpressions(Expression writerParam, Expression graphParam, Serializer serializer)
        {
            var simplfiyMethod = GetSimplifierType(mSimplifier.GetType()).GetMethod("Simplify");
            var simplify = Expression.Call(Expression.Constant(mSimplifier), simplfiyMethod, graphParam);
            var simplifiedParam = Expression.Parameter(GetSimplifiedType(mSimplifier.GetType()), "simplifiedGraph");
            var assSimplified = Expression.Assign(simplifiedParam, simplify);
            var serializeSimplified = base.GetFieldsSerializeExpressions(writerParam, simplifiedParam, serializer);
            return Expression.Block(new[] {simplifiedParam}, assSimplified, serializeSimplified);
        }

        protected override Expression GetFieldsDeserializeExpressions(Expression readerParam, Expression graphParam, Deserializer deserializer)
        {
            var simplifiedType = GetSimplifiedType(mSimplifier.GetType());
            
            var desimpliyMethod = GetSimplifierType(mSimplifier.GetType()).GetMethod("Desimplify");
            
            var simplifiedParam = Expression.Parameter(simplifiedType, "simplifiedGraph");
            var simplifiedParamInit = Expression.Assign(simplifiedParam, Expression.New(simplifiedType));
            var deserializeExpr =  base.GetFieldsDeserializeExpressions(readerParam, simplifiedParam, deserializer);
            var desimplify = Expression.Call(Expression.Constant(mSimplifier), desimpliyMethod, simplifiedParam);
            var assignment = GetAssignExpr(graphParam, desimplify);
            return Expression.Block(new[] { simplifiedParam }, simplifiedParamInit, deserializeExpr, assignment);
        }

        protected override Expression GetCreateExpression()
        {
            if (this.TypeInfo.IsAlwaysByVal)
            {
                return Expression.Default(RealType);
            }
            else if (typeof(ContextBoundObject).IsAssignableFrom(RealType))
            {
                throw new NotImplementedException("Simplified ContextBoundObjects are not supported");
            }
            else
            {
                return Expression.Convert(Expression.Call(typeof (FormatterServices), "GetUninitializedObject", null, Expression.Constant(RealType)), RealType);
            }
        }

        private Expression GetAssignExpr(Expression graphParam, Expression desimplifiedValue)
        {
            if (this.TypeInfo.IsAlwaysByVal)
            {
                return Expression.Assign(graphParam, desimplifiedValue);
            }
            else
            {
                return CopyFields(graphParam, desimplifiedValue);
            }
        }

        private Expression CopyFields(Expression graphParam, Expression desimplifiedValue)
        {
            var tempParam = Expression.Parameter(RealType, "temp");
            var assign = Expression.Assign(tempParam, desimplifiedValue);
            var fields = RealType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var expressions = new List<Expression>();
            expressions.Add(assign);
            foreach (var field in fields)
            {
                var expr = Expression.Assign(Expression.MakeMemberAccess(graphParam, field),
                    Expression.MakeMemberAccess(tempParam, field));
                expressions.Add(expr);
            }
            return Expression.Block(new []{tempParam}, expressions);
        }
    }
}