using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CoolSerializer.V3
{

    class CollecionBoundTypeInfoProvider : IBoundTypeInfoProvider
    {
        public bool TryProvide(TypeInfo info, out IBoundTypeInfo boundTypeInfo)
        {
            boundTypeInfo = null;
            if (info.RawType != FieldType.Collection)
            {
                return false;
            }
            var realType = Type.GetType(info.Name);
            if (realType == null)
            {
                return false;
            }
            boundTypeInfo = new BoundCollectionTypeInfo(info, realType);
            return true;
        }
    }

    public class BoundCollectionTypeInfo : IBoundTypeInfo
    {
        private readonly BoundCollectionFieldInfo mElementInfo;

        public BoundCollectionTypeInfo(TypeInfo info, Type realType)
        {
            TypeInfo = info;
            RealType = realType;
            mElementInfo = realType.IsArray ? new BoundArrayFieldInfo(realType) : new BoundCollectionFieldInfo(realType);
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
            var collectionAdd = mElementInfo.GetSetExpression(graphParam, castedDes, (Expression) iParam);
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

    public class BoundCollectionFieldInfo : IBoundFieldInfo
    {
        public BoundCollectionFieldInfo(Type collectionType)
        {
            Type elementType = collectionType.GetElementTypeEx();
            if (elementType == null)
            {
                elementType = typeof(object);
                IsGeneric = false;
            }
            else
            {
                elementType = elementType;
                IsGeneric = true;
            }

            RealType = elementType;
            RawType = elementType.GetRawType();
        }

        public Type RealType { get; protected set; }
        public FieldType RawType { get; protected set; }
        public virtual Expression GetGetExpression(Expression graphParam, params Expression[] indexerParameters)
        {
            throw new NotImplementedException("Collections are iterated via IEnumerable interface");
        }

        public virtual Expression GetSetExpression(Expression graphParam, Expression graphFieldValue, params Expression[] indexerParameters)
        {
            var addMethod = (IsGeneric ? typeof(ICollection<>).MakeGenericType(RealType) : typeof(IList)).GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            return Expression.Call(graphParam, addMethod, graphFieldValue);
        }

        public bool IsGeneric { get; private set; }
    }

    public class BoundArrayFieldInfo : BoundCollectionFieldInfo
    {
        public BoundArrayFieldInfo(Type collectionType) : base(collectionType)
        {
        }

        public override Expression GetSetExpression(Expression graphParam, Expression graphFieldValue, params Expression[] indexerParameters)
        {
            return Expression.Assign(Expression.ArrayAccess(graphParam, indexerParameters), graphFieldValue);
        }
    }

}