﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;

namespace CoolSerializer.V3
{
    class SimplifiedBoundTypeInfoProvider : IBoundTypeInfoProvider
    {
        private readonly ISimplifersProvider mSimplifiersProvider;
        private readonly IBoundFieldInfoProvider mFieldsProvider;

        public SimplifiedBoundTypeInfoProvider(ISimplifersProvider simplifiersProvider, IBoundFieldInfoProvider fieldsProvider)
        {
            mSimplifiersProvider = simplifiersProvider;
            mFieldsProvider = fieldsProvider;
        }

        public bool TryProvide<T>(TypeInfo info, out IBoundTypeInfo boundTypeInfo)
        {
            object simplifier;
            var realType = Type.GetType(info.Name);


            if (realType != null && mSimplifiersProvider.TryProvide(realType, out simplifier))
            {
                boundTypeInfo = new SimplifiedBoundTypeInfo(info, simplifier, GetFields(info, simplifier.GetType()));
                return true;
            }
            boundTypeInfo = null;
            return false;
        }

        private IBoundFieldInfo[] GetFields(TypeInfo typeInfo, Type simplifierType)
        {
            var type = SimplifiersHelper.GetSimplifiedType(simplifierType);
            var fields = mFieldsProvider.Provide(typeInfo, type);
            return fields;
        }

    }

    public class SimplifiedBoundTypeInfo : BoundTypeInfo
    {
        private readonly object mSimplifier;

        public SimplifiedBoundTypeInfo(TypeInfo typeInfo, object simplifier, IBoundFieldInfo[] fields)
            : base(typeInfo, SimplifiersHelper.GetRealType(simplifier.GetType()), fields)
        {
            mSimplifier = simplifier;
        }
        protected override void AddFieldsSerializeExpressions(SerializationMutationHelper helper)
        {
            var simplfiyMethod = SimplifiersHelper.GetSimplifierType(mSimplifier.GetType()).GetMethod("Simplify");
            var simplify = Expression.Call(Expression.Constant(mSimplifier), simplfiyMethod, helper.Graph);
            var simplifiedParam = Expression.Parameter(SimplifiersHelper.GetSimplifiedType(mSimplifier.GetType()), "simplifiedGraph");
            var assSimplified = Expression.Assign(simplifiedParam, simplify);
            
            helper.Variables.Add(simplifiedParam);
            helper.MethodBody.Add(assSimplified);

            var oldGraph = helper.Graph;
            helper.Graph = simplifiedParam;
            base.AddFieldsSerializeExpressions(helper);
            helper.Graph = oldGraph;
        }

        protected override void AddFieldsDeserializeExpressions(DeserializationMutationHelper helper)
        {
            var simplifiedType = SimplifiersHelper.GetSimplifiedType(mSimplifier.GetType());
            
            var desimpliyMethod = SimplifiersHelper.GetSimplifierType(mSimplifier.GetType()).GetMethod("Desimplify");
            
            var simplifiedParam = Expression.Parameter(simplifiedType, "simplifiedGraph");
            var simplifiedParamInit = Expression.Assign(simplifiedParam, Expression.New(simplifiedType));
            
            helper.Variables.Add(simplifiedParam);
            helper.MethodBody.Add(simplifiedParamInit);

            var oldGraph = helper.Graph;
            helper.Graph = simplifiedParam;
            base.AddFieldsDeserializeExpressions(helper);
            helper.Graph = oldGraph;

            var desimplify = Expression.Call(Expression.Constant(mSimplifier), desimpliyMethod, simplifiedParam);
            var assignment = GetAssignExpr(helper.Graph, desimplify);

            helper.MethodBody.Add(assignment);
        }

        protected override Expression GetCreateExpression(DeserializationMutationHelper helper)
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

    internal class SimplifiersHelper
    {
        internal static Type GetRealType(Type simplifierType)
        {
            return GetSimplifierType(simplifierType).GetGenericArguments()[0];
        }

        internal static Type GetSimplifiedType(Type simplifierType)
        {
            return GetSimplifierType(simplifierType).GetGenericArguments()[1];
        }

        internal static Type GetSimplifierType(Type simplifierType)
        {
            return simplifierType.GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISimpifier<,>));
        }
    }
}