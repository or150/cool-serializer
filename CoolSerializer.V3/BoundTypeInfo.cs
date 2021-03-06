﻿using System;
using System.Collections;
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

        public bool TryProvide<T>(TypeInfo info, out IBoundTypeInfo boundTypeInfo)
        {
            var realType = Type.GetType(info.Name);
            if (realType == null)
            {
                boundTypeInfo = null;
                return false;
            }

            var fields = mFieldsProvider.Provide(info, realType);
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
        public virtual IBoundFieldInfo[] Fields { get; private set; }


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

            var block = Expression.Block(helper.Variables,helper.MethodBody);
            return block;
        }

        protected virtual void AddFieldsSerializeExpressions(SerializationMutationHelper helper)
        {
            foreach (var field in this.Fields)
            {
                var fieldAccessExpression = field.GetGetExpression(helper.Graph);
                var serializeExpression = helper.Serializer.GetRightSerializeMethod(helper.Writer, fieldAccessExpression, field);
                helper.MethodBody.Add(serializeExpression);
            }
        }

        public Expression GetDeserializeExpression(Expression readerParam, TypeInfo info, Deserializer deserializer)
        {
            var retValParam = Expression.Variable(this.RealType, "retVal");
            var helper = new DeserializationMutationHelper(deserializer, retValParam, readerParam);
            helper.Variables.Add(retValParam);
            
            var creation = GetCreateExpression(helper);
            var retValAssignment = Expression.Assign(retValParam, creation);
            helper.MethodBody.Add(retValAssignment);

            if (RealType.IsExtraDataHolder())
            {
                var extraDataAssignment = CreateExtraDataInitializationExpression(retValParam, info);
                helper.MethodBody.Add(extraDataAssignment);
            }
            var addToVisitedObjects = deserializer.GetAddToVisitedObjectsExpr(this, retValParam);
            helper.MethodBody.Add(addToVisitedObjects);


            this.AddFieldsDeserializeExpressions(helper);

            helper.MethodBody.Add(retValParam);

            var block = Expression.Block(helper.Variables, helper.MethodBody);
            return block;
        }

        private Expression CreateExtraDataInitializationExpression(ParameterExpression graphParam, TypeInfo info)
        {
            var extraData = typeof(IExtraDataHolder).GetProperty("ExtraData");
            var extraDataCtor = typeof(ExtraData).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(TypeInfo) }, null);
            var createExtraData = Expression.New(extraDataCtor, Expression.Constant(info));
            return Expression.Assign(Expression.MakeMemberAccess(graphParam, extraData), createExtraData);
        }

        protected virtual void AddFieldsDeserializeExpressions(DeserializationMutationHelper helper)
        {
            foreach (var field in this.Fields)
            {
                var castedDes = helper.Deserializer.GetRightDeserializeMethod(helper.Reader, field);
                var assignment = field.GetSetExpression(helper.Graph, castedDes);
                helper.MethodBody.Add(assignment);
            }
        }

        protected virtual Expression GetCreateExpression(DeserializationMutationHelper helper)
        {
            return Expression.New(RealType);
        }
    }

    public class EmptyBoundFieldInfo : IBoundFieldInfo
    {
        public EmptyBoundFieldInfo(FieldInfo fieldInfo)
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

            FieldInfo = fieldInfo;

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

        public FieldInfo FieldInfo { get; set; }

        public Type RealType { get; private set; }
        public FieldType RawType { get { return FieldInfo.Type; } }
        public virtual Expression GetGetExpression(Expression graphParam, params Expression[] indexerParameters)
        {
            throw new NotSupportedException();
        }

        public virtual Expression GetSetExpression(Expression graphParam, Expression graphFieldValue, params Expression[] indexerParameters)
        {
            return graphFieldValue;
        }
    }

    public class ExtraDataBoundFieldInfo : EmptyBoundFieldInfo
    {
        private readonly int mExtraItemNumber;
        private readonly Type mExtraFieldType;
        private readonly PropertyInfo mExtraFieldValueProp;

        public ExtraDataBoundFieldInfo(FieldInfo fieldInfo, int extraItemNumber)
            : base(fieldInfo)
        {
            mExtraItemNumber = extraItemNumber;
            mExtraFieldType = typeof(ExtraField<>).MakeGenericType(RealType);
            mExtraFieldValueProp = mExtraFieldType.GetProperty("FieldValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public override Expression GetGetExpression(Expression graphParam, params Expression[] indexerParameters)
        {
            var indexerProp = typeof (List<ExtraField>).GetProperty("Item");
            var getItemFromList = Expression.MakeIndex(GetExtraFieldsListExpression(graphParam), indexerProp, new[] {Expression.Constant(mExtraItemNumber)});
            var castToRightType = Expression.Convert(getItemFromList, mExtraFieldType);
            var accessValue = Expression.MakeMemberAccess(castToRightType, mExtraFieldValueProp);
            return accessValue;
        }

        public override Expression GetSetExpression(Expression graphParam, Expression graphFieldValue, params Expression[] indexerParameters)
        {
            //TODO: check for nulls!
            var extraData = GetExtraFieldsListExpression(graphParam);
            var tempParam = Expression.Parameter(mExtraFieldType, "temp");
            var extraFieldCreation = Expression.Assign(tempParam, Expression.New(mExtraFieldType));
            var assignValue = Expression.Assign(Expression.MakeMemberAccess(tempParam, mExtraFieldValueProp), graphFieldValue);
            var addToCollection = Expression.Call(extraData, "Add", null, tempParam);
            return Expression.Block(new[] {tempParam}, extraFieldCreation, assignValue, addToCollection);
        }

        private static MemberExpression GetExtraFieldsListExpression(Expression graphParam)
        {
            var extraDataProp = typeof (IExtraDataHolder).GetProperty("ExtraData",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var extraFieldsProp = typeof (ExtraData).GetProperty("ExtraFields",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var extraData = Expression.MakeMemberAccess(Expression.MakeMemberAccess(graphParam, extraDataProp), extraFieldsProp);
            return extraData;
        }
    }
    public class BoundFieldInfo : IBoundFieldInfo
    {
        private readonly PropertyInfo mInfo;

        public BoundFieldInfo(Type objectType, FieldInfo fieldInfo, PropertyInfo propertyInfo)
        {
            mInfo = propertyInfo;
            RealType = mInfo.PropertyType;
            FieldInfo = fieldInfo;
        }

        public Type RealType { get; private set; }
        public FieldType RawType { get { return FieldInfo.Type; } }
        public FieldInfo FieldInfo { get; private set; }

        public Expression GetGetExpression(Expression graphParam, params Expression[] indexerParameters)
        {
            return Expression.MakeMemberAccess(graphParam, mInfo);
        }

        public Expression GetSetExpression(Expression graphParam, Expression graphFieldValue, params Expression[] indexerParameters)
        {
            return Expression.Assign(Expression.MakeMemberAccess(graphParam, mInfo), graphFieldValue);
        }
    }
}