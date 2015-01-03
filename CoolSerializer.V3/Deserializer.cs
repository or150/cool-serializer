using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CoolSerializer.V3
{
    public class Deserializer
    {
        TypeInfoBinder mBinder = new TypeInfoBinder();
        
        ConcurrentDictionary<TypeInfo,Delegate> mDeserializeMethods = new ConcurrentDictionary<TypeInfo, Delegate>(TypeInfoEqualityComparer.Instance);
        private List<object> mVisitedObjects;

        public object Deserialize(Stream s)
        {
            mVisitedObjects = new List<object>();
            var reader = new StreamDocumentReader(s);
            return DeserializeComplex<object>(reader);
        }

        private object Unbox(IDocumentReader reader)
        {
            var type = (FieldType)reader.ReadByte();

            switch (type)
            {
                case FieldType.Boolean:
                    return reader.ReadBoolean();
                case FieldType.Char:
                    return reader.ReadChar();
                case FieldType.SByte:
                    return reader.ReadSByte();
                case FieldType.Byte:
                    return reader.ReadByte();
                case FieldType.Int16:
                    return reader.ReadInt16();
                case FieldType.UInt16:
                    return reader.ReadUInt16();
                case FieldType.Int32:
                    return reader.ReadInt32();
                case FieldType.UInt32:
                    return reader.ReadUInt32();
                case FieldType.Int64:
                    return reader.ReadInt64();
                case FieldType.UInt64:
                    return reader.ReadUInt64();
                case FieldType.Single:
                    return reader.ReadSingle();
                case FieldType.Double:
                    return reader.ReadDouble();
                case FieldType.Decimal:
                    return reader.ReadDecimal();
                case FieldType.DateTime:
                    return reader.ReadDateTime();
                case FieldType.Guid:
                    return reader.ReadGuid();
                case FieldType.String:
                    return reader.ReadString();
                default:
                    throw new NotImplementedException();
            }
        }

        private T DeserializeComplex<T>(IDocumentReader reader)
        {
            var header = (ComplexHeader) reader.ReadByte();
            switch (header)
            {
                case ComplexHeader.Value:
                    return DeserializeValue<T>(reader);
                case ComplexHeader.Boxing:
                    return (T)Unbox(reader);
                case ComplexHeader.Null:
                    return default(T);
                case ComplexHeader.Reference:
                    return GetVisitedObject<T>(reader);
                default:
                    throw new NotSupportedException();
            }
        }

        private T GetVisitedObject<T>(IDocumentReader reader)
        {
            return (T) mVisitedObjects[reader.ReadInt32()];
        }

        private void AddToVisitedObjects<T>(T obj)
        {
            mVisitedObjects.Add(obj);
        }

        private T DeserializeValue<T>(IDocumentReader reader)
        {
            var info = reader.ReadTypeInfo();
            var deserializeMethod = (Func<IDocumentReader,T>)mDeserializeMethods.GetOrAdd(info, i => GetDeserializeExpression<T>(i).Compile());
            return deserializeMethod(reader);
        }

        private Expression<Func<IDocumentReader, T>> GetDeserializeExpression<T>(TypeInfo info)
        {
            var boundInfo = mBinder.Provide(info);
            var readerParam = Expression.Parameter(typeof(IDocumentReader), "reader");
            var block = boundInfo.GetDeserializeExpression(readerParam, this);
            if (boundInfo.RealType.IsValueType != typeof (T).IsValueType)
            {
                block = Expression.Convert(block, typeof (T));
            }
            var lambda = Expression.Lambda<Func<IDocumentReader, T>>(block, readerParam);
            return lambda;
        }


        public Expression GetAddToVisitedObjectsExpr(IBoundTypeInfo boundInfo, ParameterExpression retValParam)
        {
            return boundInfo.TypeInfo.IsAlwaysByVal ? (Expression) Expression.Empty() : Expression.Call(Expression.Constant(this), "AddToVisitedObjects", new[] {boundInfo.RealType}, retValParam);
        }

        public Expression GetRightDeserializeMethod(Expression readerParam, IBoundFieldInfo fieldType)
        {
            if (fieldType.RawType == FieldType.Object || fieldType.RawType == FieldType.Collection)
            {
                var deserializeField = Expression.Call
                    (Expression.Constant(this),
                        "DeserializeComplex",
                        new[] {fieldType.RealType},
                        readerParam);
                return deserializeField;
            }
            //else if (fieldType.FieldInfo.Type == FieldType.ObjectByVal)
            //{
            //    var fieldInfo = ((IByValBoundFieldInfo)fieldType).TypeInfo;
            //    var retValParam = Expression.Variable(fieldInfo.RealType, "retField");
            //    var creation = Expression.New(fieldInfo.RealType);
            //    return Expression.Block(new []{retValParam},new[]{creation}.Concat(GetDeserializeExpressions(fieldInfo, readerParam, retValParam)).Concat(new[]{retValParam}));
            //}
            var deserializeMethod = typeof (IDocumentReader).GetMethods
                (BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(m => m.ReturnType == fieldType.RealType);
            var deserializeExpr = Expression.Call(readerParam, deserializeMethod);
            return deserializeExpr;
        }
    }

    internal class TypeInfoEqualityComparer : IEqualityComparer<TypeInfo>
    {
        public static TypeInfoEqualityComparer Instance { get; private set; }

        static TypeInfoEqualityComparer()
        {
            Instance = new TypeInfoEqualityComparer();
        }
        private TypeInfoEqualityComparer()
        {
            
        }
        public bool Equals(TypeInfo x, TypeInfo y)
        {
            return x.Guid == y.Guid;
        }

        public int GetHashCode(TypeInfo obj)
        {
            return obj.Guid.GetHashCode();
        }
    }
}
