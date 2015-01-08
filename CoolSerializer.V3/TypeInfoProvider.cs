using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CoolSerializer.V3
{
    public interface IExtraDataHolder
    {
        ExtraData ExtraData { get; set; }
    }

    public class ExtraData
    {
        internal ExtraData()
        {
        }

        internal ExtraData(TypeInfo info)
        {
            TypeInfo = info;
            ExtraFields = new List<ExtraField>();
        }
        public TypeInfo TypeInfo { get; set; }
        public List<ExtraField> ExtraFields { get; set; }
    }

    public abstract class ExtraField
    {
    }

    internal class ExtraField<T> : ExtraField
    {
        public T FieldValue { get; set; }
    }

    public class TypeInfoProvider
    {
        private readonly ISimplifersProvider mSimplifersProvider;
        readonly ConcurrentDictionary<Type, TypeInfo> mInfos = new ConcurrentDictionary<Type, TypeInfo>(EqualityComparer<Type>.Default);
        readonly ConcurrentDictionary<TypeInfo,TypeInfo> mMergedInfos = new ConcurrentDictionary<TypeInfo, TypeInfo>(TypeInfoEqualityComparer.Instance);

        public TypeInfoProvider(ISimplifersProvider simplifersProvider)
        {
            mSimplifersProvider = simplifersProvider;
        }

        public TypeInfo Provide<T>(T graph)
        {
            var extraInfo = TypeInfoHelper<T>.GetExtraDataTypeInfo(graph);
            if (extraInfo != null)
            {
                return mMergedInfos.GetOrAdd(extraInfo, x => MergeExtraInfos(x, TypeInfoHelper<T>.ProvideType(graph)));
            }
            var type = TypeInfoHelper<T>.ProvideType(graph);
            return mInfos.GetOrAdd(type, CreateTypeInfo);
        }

        private TypeInfo MergeExtraInfos(TypeInfo arg, Type type)
        {
            var info = CreateTypeInfo(type);
            var fields = info.Fields.Union(arg.Fields, FieldEqualityComparer.Instance).ToArray();
            return new TypeInfo(info.Guid, arg.Name, arg.RawType, fields, arg.IsAlwaysByVal);
        }
        private TypeInfo CreateTypeInfo(Type t)
        {
            var rawType = t.GetRawType();
            FieldInfo[] fields = null;
            if (rawType == FieldType.Collection)
            {
                fields = new FieldInfo[]
                {
                    new FieldInfo((t.GetElementTypeEx() ?? typeof(object)).GetRawType(),"Item"), 
                };
            }
            else
            {
                fields = ProvideFields(GetRightType(t));
            }
            return new TypeInfo(Guid.NewGuid(), t.FullName, rawType, fields, t.IsValueType);
        }

        private Type GetRightType(Type type)
        {
            Type simplifiedType;
            if (mSimplifersProvider.TryGetSimplifiedType(type, out simplifiedType))
            {
                return simplifiedType;
            }
            return type;
        }

        private FieldInfo[] ProvideFields(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(f=>FieldIsNotExtraData(f,type)).Select(CreateFieldInfo).ToArray();
        }

        private bool FieldIsNotExtraData(PropertyInfo arg, Type type)
        {
            var extraDataType = typeof (IExtraDataHolder);
            if (!extraDataType.IsAssignableFrom(type))
            {
                return true;
            }
            return !type.GetInterfaceMap(extraDataType).TargetMethods.Contains(arg.GetGetMethod(true));
        }

        private FieldInfo CreateFieldInfo(PropertyInfo p)
        {
            var rawType = p.PropertyType.GetRawType();
            if (rawType == FieldType.Collection)
            {
                var elementType = p.PropertyType.GetElementTypeEx();
                return new CollectionFieldInfo(elementType.GetRawType(),p.Name);
            }
            return new FieldInfo(rawType, p.Name);
        }
    }
    internal static class TypeInfoHelper<T>
    {
        private static bool mIsClass;
        private static Type mType;
        private static bool mIsPrimitive;
        private static bool mIsExtraDataHolder;
        private static Func<T, TypeInfo> mGetExtraDataTypeInfo;

        static TypeInfoHelper()
        {
            mIsClass = !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;
            mType = typeof(T);
            mIsPrimitive = (Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T)).GetRawType() != FieldType.Object;
            mIsExtraDataHolder = typeof(IExtraDataHolder).IsAssignableFrom(typeof(T));
            if (mIsExtraDataHolder)
            {
                mGetExtraDataTypeInfo = CreateGetExtraDataTypeInfo();
            }
        }

        private static Func<T, TypeInfo> CreateGetExtraDataTypeInfo()
        {
            var param = Expression.Parameter(typeof(T));
            var extraData = Expression.MakeMemberAccess(param, typeof(IExtraDataHolder).GetProperty("ExtraData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            var typeInfo = Expression.MakeMemberAccess(extraData, typeof(ExtraData).GetProperty("TypeInfo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            var condition = Expression.ReferenceNotEqual(extraData, Expression.Constant(null, typeof(ExtraData)));
            var returnLabel = Expression.Label(typeof(TypeInfo));
            var ifThenElse = Expression.IfThenElse(condition, Expression.Return(returnLabel, typeInfo), Expression.Return(returnLabel, Expression.Constant(null, typeof(TypeInfo))));
            var lambda = Expression.Lambda<Func<T, TypeInfo>>(Expression.Block(ifThenElse, Expression.Label(returnLabel, Expression.Constant(null, typeof(TypeInfo)))), new[] { param });
            return lambda.Compile();
        }

        public static Type ProvideType(T graph)
        {
            return mIsClass && graph != null ? graph.GetType() : mType;
        }

        public static TypeInfo GetExtraDataTypeInfo(T graph)
        {
            if (mIsClass && graph != null)
            {
                var holder = graph as IExtraDataHolder;
                if (holder != null && holder.ExtraData != null)
                {
                    return holder.ExtraData.TypeInfo;
                }
            }
            if (mIsExtraDataHolder)
            {
                return mGetExtraDataTypeInfo(graph);
            }
            return null;
        }

    }

    class FieldEqualityComparer : IEqualityComparer<FieldInfo>
    {
        public static FieldEqualityComparer Instance { get; private set; }

        static FieldEqualityComparer()
        {
            Instance = new FieldEqualityComparer();
        }

        private FieldEqualityComparer()
        {

        }
        public bool Equals(FieldInfo x, FieldInfo y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode(FieldInfo obj)
        {
            return obj.Name.GetHashCode();
        }
    }
}
