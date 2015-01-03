using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CoolSerializer.V3
{
    public class TypeInfoAssemblyBinder
    {
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

            var fields = new IBoundFieldInfo[info.Fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = CreateBoundedFieldInfo(realType, info.Fields[i]);
            }
            return new BoundTypeInfo(info, realType, fields);
        }

        private IBoundFieldInfo CreateBoundedFieldInfo(Type objectType, FieldInfo fieldInfo)
        {
            return new BoundFieldInfo(objectType, fieldInfo);
        }
    }

    public class BoundCollectionTypeInfo : IBoundTypeInfo
    {
        private BoundCollectionFieldInfo mElementInfo;

        public BoundCollectionTypeInfo(TypeInfo info, Type realType)
        {
            TypeInfo = info;
            RealType = realType;
            mElementInfo = new BoundCollectionFieldInfo(realType, info.Fields[0]);
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

        public IEnumerable<Expression> GetFieldsDeserializeExpressions(Expression readerParam, Expression graphParam,
            Func<Expression, IBoundFieldInfo, Expression> deserializeMethodProvider)
        {
            var countParam = Expression.Parameter(typeof (int), "count");
            var readInt32 = Expression.Assign(countParam,Expression.Call(readerParam, "ReadInt32", null));
            var iParam = Expression.Parameter(typeof (int), "i");
            var range = new Func<int, int, IEnumerable<int>>(Enumerable.Range).Method;
            var castedDes = deserializeMethodProvider(readerParam, mElementInfo);
            var collectionAdd = GetAddMethod(graphParam, iParam,castedDes);
            var iteration = ForEach(typeof (int), Expression.Call(range, Expression.Constant(0), countParam), iParam, collectionAdd);
            var block = Expression.Block(new[] {countParam, iParam}, readInt32, Expression.Assign(graphParam, GetCreateMethod(countParam)), iteration);
            yield return block;
        }

        private Expression GetCreateMethod(Expression count)
        {
            if (RealType.IsArray)
            {
                return Expression.NewArrayBounds(mElementInfo.RealType, count);
            }
            return Expression.New(RealType);
        }

        private Expression GetAddMethod(Expression collectionParam, Expression iParam, Expression itemParam)
        {
            if (RealType.IsArray)
            {
                return Expression.Assign(Expression.ArrayAccess(collectionParam, iParam), itemParam);
            }
            var addMethod = typeof (ICollection<>).MakeGenericType(mElementInfo.RealType).GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            return Expression.Call(collectionParam, addMethod, itemParam);
        }

        public IEnumerable<Expression> GetFieldsSerializeExpressions(Expression writerParam, Expression graphParam, Func<Expression, Expression, IBoundFieldInfo, Expression> serializeMethodProvider)
        {
            var count = Expression.MakeMemberAccess(graphParam,
                typeof (ICollection<>).MakeGenericType(mElementInfo.RealType)
                    .GetProperty("Count", BindingFlags.Instance | BindingFlags.Public));
            yield return Expression.Call(writerParam, "WriteInt32", null, count);
            var elementParam = Expression.Variable(mElementInfo.RealType, "element");
            var serializeExpression = serializeMethodProvider(writerParam, elementParam, mElementInfo);
            yield return Expression.Block(new[]{elementParam}, ForEach(mElementInfo.RealType, graphParam, elementParam, serializeExpression));
        }

        public Expression ForEach(Type elementType, Expression collection, Expression element, Expression content)
        {
            var getEnumerator = typeof (IEnumerable<>).MakeGenericType(elementType)
                .GetMethod("GetEnumerator", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);

            var enumeratorType = typeof (IEnumerator<>).MakeGenericType(elementType);
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
        public Expression GetCreateExpression()
        {
            return Expression.Default(RealType);
        }
    }

    public class BoundCollectionFieldInfo : IBoundFieldInfo
    {
        public BoundCollectionFieldInfo(Type collectionType, FieldInfo fieldInfo)
        {
            FieldInfo = fieldInfo;
            var collectionInterface = collectionType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
            var elementType = collectionInterface.GetGenericArguments()[0];
            RealType = elementType;
        }

        public Type RealType { get; private set; }
        public FieldInfo FieldInfo { get; private set; }
        public Expression GetGetExpression(Expression graphParam)
        {
            throw new NotSupportedException();
        }

        public Expression GetSetExpression(Expression graphParam, Expression graphFieldValue)
        {
            throw new NotSupportedException();
        }
    }

    public interface IBoundTypeInfo
    {
        Type RealType { get; }
        TypeInfo TypeInfo { get; }
        IBoundFieldInfo[] Fields { get; }
        IEnumerable<Expression> GetFieldsDeserializeExpressions(Expression readerParam, Expression graphParam, Func<Expression, IBoundFieldInfo, Expression> deserializeMethodProvider);
        IEnumerable<Expression> GetFieldsSerializeExpressions(Expression writerParam, Expression graphParam, Func<Expression, Expression, IBoundFieldInfo, Expression> serializeMethodProvider);
        Expression GetCreateExpression();
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

        public IEnumerable<Expression> GetFieldsDeserializeExpressions(Expression readerParam, Expression graphParam,
            Func<Expression, IBoundFieldInfo, Expression> deserializeMethodProvider)
        {
            var fieldDeserializeExprs = new List<Expression>();

            foreach (var field in this.Fields)
            {
                var castedDes = deserializeMethodProvider(readerParam, field);
                var assignment = field.GetSetExpression(graphParam, castedDes);
                fieldDeserializeExprs.Add(assignment);
            }
            return fieldDeserializeExprs;
        }

        public IEnumerable<Expression> GetFieldsSerializeExpressions(Expression writerParam, Expression graphParam, Func<Expression, Expression, IBoundFieldInfo, Expression> serializeMethodProvider)
        {
            var fieldSerializeExprs = new List<Expression>();

            foreach (var field in this.Fields)
            {
                var fieldAccessExpression = field.GetGetExpression(graphParam);
                var serializeExpression = serializeMethodProvider(writerParam, fieldAccessExpression, field);
                fieldSerializeExprs.Add(serializeExpression);
            }
            return fieldSerializeExprs;
        }

        public Expression GetCreateExpression()
        {
            return Expression.New(RealType);
        }
    }

    public interface IBoundFieldInfo
    {
        Type RealType { get; }
        FieldInfo FieldInfo { get; }
        Expression GetGetExpression(Expression graphParam);
        Expression GetSetExpression(Expression graphParam, Expression graphFieldValue);
    }

    public interface IByValBoundFieldInfo : IBoundFieldInfo
    {
        IBoundTypeInfo TypeInfo { get; }
    }

    public class BoundFieldInfo : IBoundFieldInfo
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