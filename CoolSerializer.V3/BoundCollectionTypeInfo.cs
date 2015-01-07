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
        public Expression GetDeserializeExpression(Expression readerParam,TypeInfo info, Deserializer deserializer)
        {
            var retValParam = Expression.Variable(this.RealType, "retVal");
            var helper = new DeserializationMutationHelper(deserializer, retValParam, readerParam);
            helper.Variables.Add(retValParam);

            var creation = GetCreateExpression(helper);
            var retValAssignment = Expression.Assign(retValParam, creation);
            helper.MethodBody.Add(retValAssignment);


            var addToVisitedObjects = deserializer.GetAddToVisitedObjectsExpr(this, retValParam);
            helper.MethodBody.Add(addToVisitedObjects);

            this.AddFieldsDeserializeExpressions(helper);

            helper.MethodBody.Add(retValParam);

            var block = Expression.Block(helper.Variables, helper.MethodBody);
            return block;
        }

        public void AddFieldsDeserializeExpressions(DeserializationMutationHelper helper)
        {
            var iParam = Expression.Parameter(typeof (int), "i");

            var range = new Func<int, int, IEnumerable<int>>(Enumerable.Range).Method;
            var castedDes = helper.Deserializer.GetRightDeserializeMethod(helper.Reader, mElementInfo);
            var collectionAdd = mElementInfo.GetSetExpression(helper.Graph, castedDes, (Expression) iParam);
            var iteration = ForEach(typeof(int), Expression.Call(range, Expression.Constant(0), (Expression)helper.ExtraData["CollectionCountParam"]), iParam, collectionAdd);

            helper.Variables.Add(iParam);
            helper.MethodBody.Add(iteration);
        }

        private Expression GetCreateExpression(DeserializationMutationHelper helper)
        {
            var countParam = Expression.Parameter(typeof(int), "count");
            var readInt32 = Expression.Assign(countParam, Expression.Call(helper.Reader, "ReadInt32", null));
            helper.Variables.Add(countParam);
            helper.MethodBody.Add(readInt32);
            helper.ExtraData["CollectionCountParam"] = countParam;

            if (RealType.IsArray)
            {
                return Expression.NewArrayBounds(mElementInfo.RealType, countParam);
            }
            return Expression.New(RealType);
        }

        public void AddFieldsSerializeExpressions(SerializationMutationHelper helper)
        {
            var countPropertyInfo = (mElementInfo.IsGeneric ? typeof(ICollection<>).MakeGenericType(mElementInfo.RealType) : typeof(ICollection))
                .GetProperty("Count", BindingFlags.Instance | BindingFlags.Public);
            var count = Expression.MakeMemberAccess(helper.Graph, countPropertyInfo);
            var writeCount = Expression.Call(helper.Writer, "WriteInt32", null, count);
            helper.MethodBody.Add(writeCount);


            var elementParam = Expression.Variable(mElementInfo.RealType, "element");
            var serializeExpression = helper.Serializer.GetRightSerializeMethod(helper.Writer, elementParam, mElementInfo);
            
            var forEach = ForEach(mElementInfo.IsGeneric ? mElementInfo.RealType : null, helper.Graph, elementParam, serializeExpression);

            helper.Variables.Add(elementParam);
            helper.MethodBody.Add(forEach);
        }

        public Expression GetSerializeExpression(Expression graphParam, Expression writerParam, Serializer serializer)
        {
            var castedGraph = Expression.Variable(this.RealType, "castedGraph");
            var helper = new SerializationMutationHelper(serializer, castedGraph, writerParam);
            helper.Variables.Add(castedGraph);

            var castExpression = Expression.Assign(castedGraph, Expression.Convert(graphParam, this.RealType));
            var writeHeaderExpr = Expression.Call(writerParam, "WriteByte", null,
                Expression.Constant((byte)ComplexHeader.Value));
            var writeTypeInfoExpr = Expression.Call(writerParam, "WriteTypeInfo", null, Expression.Constant(TypeInfo));

            helper.MethodBody.Add(castExpression);
            helper.MethodBody.Add(writeHeaderExpr);
            helper.MethodBody.Add(writeTypeInfoExpr);

            AddFieldsSerializeExpressions(helper);

            var block = Expression.Block(helper.Variables, helper.MethodBody);
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