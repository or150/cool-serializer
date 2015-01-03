using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CoolSerializer.V3
{
    public class TypeInfoBinder
    {
        private readonly ISimplifersProvider mSimplifersProvider;

        public TypeInfoBinder(ISimplifersProvider simplifersProvider)
        {
            mSimplifersProvider = simplifersProvider;
        }
        public IBoundTypeInfo Provide(TypeInfo info)
        {
            var realType = Type.GetType(info.Name);
            if (realType == null)
            {
                throw new NotImplementedException("Unk types are not implemented atm");
            }
            if (info.RawType == FieldType.Collection)
            {
                return new BoundCollectionTypeInfo(info, realType);
            }

            object simplifer;
            if (mSimplifersProvider.TryProvide(realType, out simplifer))
            {
                return new SimplifiedBoundTypeInfo(info,simplifer);
            }

            var fields = new IBoundFieldInfo[info.Fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = CreateBoundFieldInfo(realType, info.Fields[i]);
            }
            return new BoundTypeInfo(info, realType, fields);
        }

        private IBoundFieldInfo CreateBoundFieldInfo(Type objectType, FieldInfo fieldInfo)
        {
            return new BoundFieldInfo(objectType, fieldInfo);
        }
    }

    public interface IBoundTypeInfo
    {
        Type RealType { get; }
        TypeInfo TypeInfo { get; }
        Expression GetSerializeExpression(Expression graphParam, Expression writerParam, Serializer serializer);
        Expression GetDeserializeExpression(Expression readerParam, Deserializer deserializer);
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


        public Expression GetSerializeExpression(Expression graphParam, Expression writerParam, Serializer serializer)
        {
            var castedGraph = Expression.Variable(this.RealType, "castedGraph");
            var castExpression = Expression.Assign(castedGraph, Expression.Convert(graphParam, this.RealType));
            var writeHeaderExpr = Expression.Call(writerParam, "WriteByte", null,
                Expression.Constant((byte)ComplexHeader.Value));
            var writeTypeInfoExpr = Expression.Call(writerParam, "WriteTypeInfo", null, Expression.Constant(TypeInfo));

            var fieldSerializeExprs = this.GetFieldsSerializeExpressions(writerParam, castedGraph, serializer);

            var block = Expression.Block(new[] { castedGraph },
                new Expression[] { castExpression, writeHeaderExpr, writeTypeInfoExpr }.Concat(new []{fieldSerializeExprs}));
            return block;
        }

        public Expression GetDeserializeExpression(Expression readerParam, Deserializer deserializer)
        {
            var retValParam = Expression.Variable(this.RealType, "retVal");
            var creation = this.GetCreateExpression();
            var retValAssignment = Expression.Assign(retValParam, creation);
            var addToVisitedObjects = deserializer.GetAddToVisitedObjectsExpr(this, retValParam);
            var fieldDeserializeExprs = this.GetFieldsDeserializeExpressions(readerParam, retValParam, deserializer);

            var block = Expression.Block(new[] { retValParam },
                new[] { retValAssignment, addToVisitedObjects }.Concat(new []{fieldDeserializeExprs}).Concat(new[] { retValParam }));
            return block;
        }

        protected virtual Expression GetFieldsDeserializeExpressions(Expression readerParam, Expression graphParam, Deserializer deserializer)
        {
            var fieldDeserializeExprs = new List<Expression>();

            foreach (var field in this.Fields.Cast<IConcreteBoundFieldInfo>())
            {
                var castedDes = deserializer.GetRightDeserializeMethod(readerParam, field);
                var assignment = field.GetSetExpression(graphParam, castedDes);
                fieldDeserializeExprs.Add(assignment);
            }
            return Expression.Block(fieldDeserializeExprs);
        }

        protected virtual Expression GetCreateExpression()
        {
            return Expression.New(RealType);
        }

        protected virtual Expression GetFieldsSerializeExpressions(Expression writerParam, Expression graphParam, Serializer serializer)
        {
            var fieldSerializeExprs = new List<Expression>();

            foreach (var field in this.Fields.Cast<IConcreteBoundFieldInfo>())
            {
                var fieldAccessExpression = field.GetGetExpression(graphParam);
                var serializeExpression = serializer.GetRightSerializeMethod(writerParam, fieldAccessExpression, field);
                fieldSerializeExprs.Add(serializeExpression);
            }
            return Expression.Block(fieldSerializeExprs);
        }

    }

    public interface IBoundFieldInfo
    {
        Type RealType { get; }
        FieldType RawType { get; }
        //FieldInfo FieldInfo { get; }

    }

    public interface IConcreteBoundFieldInfo : IBoundFieldInfo
    {
        Expression GetGetExpression(Expression graphParam);
        Expression GetSetExpression(Expression graphParam, Expression graphFieldValue);
    }

    public interface IVariableBoundFieldInfo : IBoundFieldInfo
    {
        //Expression GetGetExpression(Expression graphParam, Expression iParam);
        //Expression GetSetExpression(Expression graphParam, Expression graphFieldValue, Expression iParam);
    }
    public class BoundFieldInfo : IConcreteBoundFieldInfo
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
        public FieldType RawType { get { return FieldInfo.Type; } }
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
}