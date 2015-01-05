using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CoolSerializer.V3
{
    class BasicBoundTypeInfoProvider : IBoundTypeInfoProvider
    {
        private readonly IBoundFieldInfoProvider mFieldsProvider;

        public BasicBoundTypeInfoProvider(IBoundFieldInfoProvider fieldsProvider)
        {
            mFieldsProvider = fieldsProvider;
        }

        public bool TryProvide(TypeInfo info, out IBoundTypeInfo boundTypeInfo)
        {
            var realType = Type.GetType(info.Name);
            if (realType == null)
            {
                boundTypeInfo = null;
                return false;
            }

            var fields = mFieldsProvider.Provide(info,realType);
            boundTypeInfo = new BoundTypeInfo(info, realType, fields);
            return true;
        }
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

            foreach (var field in this.Fields)
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

            foreach (var field in this.Fields)
            {
                var fieldAccessExpression = field.GetGetExpression(graphParam);
                var serializeExpression = serializer.GetRightSerializeMethod(writerParam, fieldAccessExpression, field);
                fieldSerializeExprs.Add(serializeExpression);
            }
            return Expression.Block(fieldSerializeExprs);
        }

    }
    public class BoundFieldInfo : IBoundFieldInfo
    {
        private readonly PropertyInfo mInfo;

        public BoundFieldInfo(Type objectType, FieldInfo fieldInfo)
        {
            mInfo = objectType.GetProperty(fieldInfo.Name);
            if (mInfo == null)
            {
                if (fieldInfo.Type.IsComplex())
                {
                    RealType = typeof(object); //TODO: make it better
                }
                else
                {
                    RealType = Type.GetType("System." + fieldInfo.Type.ToString("G"));
                    if (RealType == null)
                    {
                        throw new Exception("WTF?");
                    }
                }
                //if (fieldInfo.Type == FieldType.Collection)
                //{
                //    RealType = typeof(ICollection<>).MakeGenericType(Type.GetType("System." + ((CollectionFieldInfo)fieldInfo).ElementType.ToString("G")));
                //}
                //else
                //{
                //    RealType = Type.GetType("System." + fieldInfo.Type.ToString("G"));
                //    if (RealType == null)
                //    {
                //        throw new Exception("WTF?");
                //    }
                //}
            }
            else
            {
                RealType = mInfo.PropertyType;
            }
            FieldInfo = fieldInfo;
        }

        public Type RealType { get; private set; }
        public FieldType RawType { get { return FieldInfo.Type; } }
        public FieldInfo FieldInfo { get; private set; }

        public Expression GetGetExpression(Expression graphParam, params Expression[] indexerParameters)
        {
            if (mInfo == null)
            {
                throw new NotSupportedException();
            }
            return Expression.MakeMemberAccess(graphParam, mInfo);
        }

        public Expression GetSetExpression(Expression graphParam, Expression graphFieldValue, params Expression[] indexerParameters)
        {
            if (mInfo == null)
            {
                return graphFieldValue;
            }
            return Expression.Assign(Expression.MakeMemberAccess(graphParam, mInfo), graphFieldValue);
        }
    }
}