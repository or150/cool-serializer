﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace CoolSerializer.V3
{
    public class Serializer
    {
        private readonly TypeInfoProvider mProvider = new TypeInfoProvider();
        private readonly TypeInfoBinder mBinder = new TypeInfoBinder();
        private ConcurrentDictionary<TypeInfo, Delegate> mSerializeMethods = new ConcurrentDictionary<TypeInfo, Delegate>(TypeInfoEqualityComparer.Instance);
        private Dictionary<object, int> mVisitedObjects;

        public void Serialize(Stream stream, object graph)
        {
            mVisitedObjects = new Dictionary<object, int>();
            mVisitedObjects.Clear();
            var writer = new StreamDocumentWriter(stream);
            SerializeComplex(writer, graph);
        }

        private void Box(IDocumentWriter writer, object graph, FieldType expectedType)
        {
            writer.WriteByte((byte)ComplexHeader.Boxing);
            writer.WriteByte((byte)expectedType);

            switch (expectedType)
            {
                case FieldType.Boolean:
                    writer.WriteBoolean((Boolean)graph);
                    break;
                case FieldType.Char:
                    writer.WriteChar((Char)graph);
                    break;
                case FieldType.SByte:
                    writer.WriteSByte((SByte)graph);
                    break;
                case FieldType.Byte:
                    writer.WriteByte((Byte)graph);
                    break;
                case FieldType.Int16:
                    writer.WriteInt16((Int16)graph);
                    break;
                case FieldType.UInt16:
                    writer.WriteUInt16((UInt16)graph);
                    break;
                case FieldType.Int32:
                    writer.WriteInt32((Int32)graph);
                    break;
                case FieldType.UInt32:
                    writer.WriteUInt32((UInt32)graph);
                    break;
                case FieldType.Int64:
                    writer.WriteInt64((Int64)graph);
                    break;
                case FieldType.UInt64:
                    writer.WriteUInt64((UInt64)graph);
                    break;
                case FieldType.Single:
                    writer.WriteSingle((Single)graph);
                    break;
                case FieldType.Double:
                    writer.WriteDouble((Double)graph);
                    break;
                case FieldType.Decimal:
                    writer.WriteDecimal((Decimal)graph);
                    break;
                case FieldType.DateTime:
                    writer.WriteDateTime((DateTime)graph);
                    break;
                case FieldType.Guid:
                    writer.WriteGuid((Guid)graph);
                    break;
                case FieldType.String:
                    writer.WriteString((String)graph);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void SerializeComplex<T>(IDocumentWriter writer, T graph)
        {
            var info = mProvider.Provide(graph);
            if (info.RawType != FieldType.Object && info.RawType != FieldType.Collection)
            {
                Box(writer, graph, info.RawType);
                return;
            }

            if (graph == null)
            {
                writer.WriteByte((byte)ComplexHeader.Null);
                return;
            }

            if (!info.IsAlwaysByVal && WriteRevisited(writer, graph))
            {
                return;
            }

            var serializeMethod = (Action<IDocumentWriter, T>)mSerializeMethods.GetOrAdd(info, i => GetSerializeExpression<T>(i).Compile());
            serializeMethod(writer, graph);
        }

        private bool WriteRevisited<T>(IDocumentWriter writer, T graph)
        {
            int id;
            if (mVisitedObjects.TryGetValue(graph, out id))
            {
                writer.WriteByte((byte)ComplexHeader.Reference);
                writer.WriteInt32(id);
                return true;
            }
            mVisitedObjects[graph] = mVisitedObjects.Count;
            return false;
        }

        private Expression<Action<IDocumentWriter, T>> GetSerializeExpression<T>(TypeInfo info)
        {

            // Should look like this:
            //void serializeFunc(IDocumentWriter writer, {T} graph)
            //{
            //    writer.WriteByte(ComplexHeader.Value);
            //    writer.WriteTypeInfo(info);
            //    writer.WriteX(graph.fieldX);
            //    writer.WriteY(graph.fieldY);
            //    SerializeComplex(graph.ComplexField);
            //}

            var boundInfo = mBinder.Provide(info);
            var writerParam = Expression.Parameter(typeof(IDocumentWriter), "writer");
            var graphParam = Expression.Parameter(typeof(T), "graph");
            var castedGraph = Expression.Variable(boundInfo.RealType, "castedGraph");
            var castExpression = Expression.Assign(castedGraph, Expression.Convert(graphParam, boundInfo.RealType));
            var writeHeaderExpr = Expression.Call(writerParam, "WriteByte", null, Expression.Constant((byte)ComplexHeader.Value));
            var writeTypeInfoExpr = Expression.Call(writerParam, "WriteTypeInfo", null, Expression.Constant(info));

            var fieldSerializeExprs = boundInfo.GetFieldsSerializeExpressions(writerParam, castedGraph, this);

            var block = Expression.Block(new[] { castedGraph },
                new Expression[] { castExpression, writeHeaderExpr, writeTypeInfoExpr }.Concat(fieldSerializeExprs));
            var lambda = Expression.Lambda<Action<IDocumentWriter, T>>(block, writerParam, graphParam);
            return lambda;
        }

        public Expression GetRightSerializeMethod(Expression writerParam, Expression fieldExpression, IBoundFieldInfo fieldType)
        {
            var rawType = fieldType.RawType;
            if (rawType == FieldType.Object || rawType == FieldType.Collection)
            {
                var serializeField = Expression.Call
                   (Expression.Constant(this),
                       "SerializeComplex",
                       new[] { fieldType.RealType },
                       writerParam,
                       fieldExpression);
                return serializeField;
            }
            //else if (rawType == FieldType.ObjectByVal)
            //{
            //    var fieldInfo = ((IByValBoundFieldInfo) fieldType).TypeInfo;
            //    return Expression.Block(GetSerializeExpressions(fieldInfo, writerParam, fieldExpression));
            //}

            var serializeMethod = typeof(IDocumentWriter).GetMethods
                (BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(m => m.GetParameters()[0].ParameterType == fieldType.RealType);
            var serializeExpression = Expression.Call(writerParam, serializeMethod, fieldExpression);
            return serializeExpression;
        }
    }
}
