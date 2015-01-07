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

    public class BoundCollectionTypeInfo : BoundTypeInfo
    {
        private readonly BoundCollectionFieldInfo mElementInfo;

        public BoundCollectionTypeInfo(TypeInfo info, Type realType) 
            : base(info,realType,null)
        {
            mElementInfo = realType.IsArray ? new BoundArrayFieldInfo(realType) : new BoundCollectionFieldInfo(realType);
        }

        public override IBoundFieldInfo[] Fields
        {
            get
            {
                throw new NotSupportedException("Collection does not have fields");
            }
        }

        protected override void AddFieldsDeserializeExpressions(DeserializationMutationHelper helper)
        {
            var iParam = Expression.Parameter(typeof (int), "i");

            var range = new Func<int, int, IEnumerable<int>>(Enumerable.Range).Method;
            var castedDes = helper.Deserializer.GetRightDeserializeMethod(helper.Reader, mElementInfo);
            var collectionAdd = mElementInfo.GetSetExpression(helper.Graph, castedDes, (Expression) iParam);
            var iteration = ExpressionHelpers.ForEach(typeof(int), Expression.Call(range, Expression.Constant(0), (Expression)helper.ExtraData["CollectionCountParam"]), iParam, collectionAdd);

            helper.Variables.Add(iParam);
            helper.MethodBody.Add(iteration);
        }

        protected override Expression GetCreateExpression(DeserializationMutationHelper helper)
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

        protected override void AddFieldsSerializeExpressions(SerializationMutationHelper helper)
        {
            var countPropertyInfo = (mElementInfo.IsGeneric ? typeof(ICollection<>).MakeGenericType(mElementInfo.RealType) : typeof(ICollection))
                .GetProperty("Count", BindingFlags.Instance | BindingFlags.Public);
            var count = Expression.MakeMemberAccess(helper.Graph, countPropertyInfo);
            var writeCount = Expression.Call(helper.Writer, "WriteInt32", null, count);
            helper.MethodBody.Add(writeCount);


            var elementParam = Expression.Variable(mElementInfo.RealType, "element");
            var serializeExpression = helper.Serializer.GetRightSerializeMethod(helper.Writer, elementParam, mElementInfo);
            
            var forEach = ExpressionHelpers.ForEach(mElementInfo.IsGeneric ? mElementInfo.RealType : null, helper.Graph, elementParam, serializeExpression);

            helper.Variables.Add(elementParam);
            helper.MethodBody.Add(forEach);
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