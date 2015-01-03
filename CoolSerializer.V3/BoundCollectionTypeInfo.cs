using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CoolSerializer.V3
{
    public class BoundCollectionTypeInfo : IBoundTypeInfo
    {
        private readonly BoundCollectionFieldInfo mElementInfo;

        public BoundCollectionTypeInfo(TypeInfo info, Type realType)
        {
            TypeInfo = info;
            RealType = realType;
            mElementInfo = new BoundCollectionFieldInfo(realType);
        }

        public Type RealType { get; private set; }
        public TypeInfo TypeInfo { get; private set; }
        public IBoundFieldInfo[] Fields
        {
            get
            {
                throw new NotSupportedException("Collection does not have fields");
            }
        }
        public Expression GetDeserializeExpression(Expression readerParam, Deserializer deserializer)
        {
            var countParam = Expression.Parameter(typeof(int), "count");
            var readInt32 = Expression.Assign(countParam, Expression.Call(readerParam, "ReadInt32", null));
            var retValParam = Expression.Variable(this.RealType, "retVal");
            var creation = this.GetCreateExpression(countParam);
            var retValAssignment = Expression.Assign(retValParam, creation);
            var addToVisitedObjects = deserializer.GetAddToVisitedObjectsExpr(this, retValParam);
            var fieldDeserializeExprs = this.GetFieldsDeserializeExpressions(readerParam, retValParam, deserializer,countParam);

            var block = Expression.Block(new[] { retValParam,countParam },
                new[] { readInt32, retValAssignment, addToVisitedObjects }.Concat(fieldDeserializeExprs).Concat(new[] { retValParam }));
            return block;
        }

        public IEnumerable<Expression> GetFieldsDeserializeExpressions(Expression readerParam, Expression graphParam,Deserializer deserializer, Expression countParam)
        {
            var iParam = Expression.Parameter(typeof (int), "i");
            var range = new Func<int, int, IEnumerable<int>>(Enumerable.Range).Method;
            var castedDes = deserializer.GetRightDeserializeMethod(readerParam, mElementInfo);
            var collectionAdd = GetAddExpression(graphParam, iParam,castedDes);
            var iteration = ForEach(typeof (int), Expression.Call(range, Expression.Constant(0), countParam), iParam, collectionAdd);
            var block = Expression.Block(new[] {iParam}, iteration);
            yield return block;
        }

        private Expression GetCreateExpression(Expression count)
        {
            if (RealType.IsArray)
            {
                return Expression.NewArrayBounds(mElementInfo.RealType, count);
            }
            return Expression.New(RealType);
        }

        private Expression GetAddExpression(Expression collectionParam, Expression iParam, Expression itemParam)
        {
            if (RealType.IsArray)
            {
                return Expression.Assign(Expression.ArrayAccess(collectionParam, iParam), itemParam);
            }
            var addMethod = (mElementInfo.IsGeneric ? typeof (ICollection<>).MakeGenericType(mElementInfo.RealType) : typeof(IList)).GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            return Expression.Call(collectionParam, addMethod, itemParam);
        }

        public IEnumerable<Expression> GetFieldsSerializeExpressions(Expression writerParam, Expression graphParam, Serializer serializer)
        {
            var countPropertyInfo = (mElementInfo.IsGeneric ? typeof(ICollection<>).MakeGenericType(mElementInfo.RealType) : typeof(ICollection))
                .GetProperty("Count", BindingFlags.Instance | BindingFlags.Public);
            var count = Expression.MakeMemberAccess(graphParam,
                countPropertyInfo);
            yield return Expression.Call(writerParam, "WriteInt32", null, count);
            var elementParam = Expression.Variable(mElementInfo.RealType, "element");
            var serializeExpression = serializer.GetRightSerializeMethod(writerParam, elementParam, mElementInfo);
            yield return Expression.Block(new[]{elementParam}, ForEach(mElementInfo.IsGeneric ? mElementInfo.RealType : null, graphParam, elementParam, serializeExpression));
        }

        public Expression GetSerializeExpression(Expression graphParam, Expression writerParam, Serializer serializer)
        {
            var castedGraph = Expression.Variable(this.RealType, "castedGraph");
            var castExpression = Expression.Assign(castedGraph, Expression.Convert(graphParam, this.RealType));
            var writeHeaderExpr = Expression.Call(writerParam, "WriteByte", null,
                Expression.Constant((byte)ComplexHeader.Value));
            var writeTypeInfoExpr = Expression.Call(writerParam, "WriteTypeInfo", null, Expression.Constant(TypeInfo));

            var fieldSerializeExprs = this.GetFieldsSerializeExpressions(writerParam, castedGraph, serializer);

            var block = Expression.Block(new[] { castedGraph },
                new Expression[] { castExpression, writeHeaderExpr, writeTypeInfoExpr }.Concat(fieldSerializeExprs));
            return block;
        }

        public Expression ForEach(Type elementType, Expression collection, Expression element, Expression content)
        {
            var getEnumerator = (elementType == null ? typeof(IEnumerable) : typeof (IEnumerable<>).MakeGenericType(elementType))
                .GetMethod("GetEnumerator", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);

            var enumeratorType = (elementType == null ? typeof(IEnumerator) : typeof (IEnumerator<>).MakeGenericType(elementType));
            var enumerator = Expression.Variable(enumeratorType, "enumerator");
            var moveNext = typeof(IEnumerator)
                .GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public);
            var current = enumeratorType
                .GetProperty("Current", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);

            var breakLabel = Expression.Label("breakLabel");
            var enumratorAssignment = Expression.Assign(enumerator, Expression.Call(collection, getEnumerator));
            var forEach = 
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.Call(enumerator,moveNext), // Condition
                        Expression.Block(typeof(void), Expression.Assign(element, Expression.MakeMemberAccess(enumerator,current)),content), // if true
                        Expression.Break(breakLabel,typeof(void))) // else
                    , breakLabel);
            var block =  Expression.Block(new[] {enumerator}, enumratorAssignment, forEach);
            return block;
        }
    }

    public class BoundCollectionFieldInfo : IVariableBoundFieldInfo
    {
        public BoundCollectionFieldInfo(Type collectionType)
        {
            var collectionInterface = collectionType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));

            Type elementType = null;
            if (collectionInterface == null)
            {
                if (collectionType.GetInterfaces().All(i => i != typeof(IList)))
                {
                    throw new NotSupportedException();
                }
                elementType = typeof(object);
                IsGeneric = false;
            }
            else
            {
                elementType = collectionInterface.GetGenericArguments()[0];
                IsGeneric = true;
            }

            RealType = elementType;
            RawType = elementType.GetRawType();
        }

        public Type RealType { get; private set; }
        public FieldType RawType { get; private set; }
        public FieldInfo FieldInfo { get; private set; }
        public bool IsGeneric { get; private set; }
    }

}