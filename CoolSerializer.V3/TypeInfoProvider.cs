using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CoolSerializer.V3
{
    public class TypeInfoProvider
    {
        readonly ConcurrentDictionary<Type,TypeInfo> mInfos = new ConcurrentDictionary<Type, TypeInfo>(EqualityComparer<Type>.Default);
        static class TypeInfoHelper<T>
        {
            private static FieldType mRawType;
            private static bool mIsClass;
            private static Type mType;

            static TypeInfoHelper()
            {
                mRawType = typeof (T).GetRawType();
                mIsClass = !typeof (T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;
                mType = typeof (T);
            }

            public static Type ProvideType(T graph)
            {
                return mIsClass && graph != null ? graph.GetType() : mType;
            }
        }
        public TypeInfo Provide<T>(T graph)
        {
            var type = TypeInfoHelper<T>.ProvideType(graph);
            return mInfos.GetOrAdd(type, CreateTypeInfo);
        }

        private TypeInfo CreateTypeInfo(Type t)
        {
            var rawType = t.GetRawType();
            FieldInfo[] fields = null;
            if (t.GetRawType() == FieldType.Collection)
            {
                fields = new FieldInfo[0];
            }
            else
            {
                fields = ProvideFields(t);
            }
            return new TypeInfo(Guid.NewGuid(), t.FullName, rawType, fields, t.IsValueType);
        }

        private FieldInfo[] ProvideFields(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(CreateFieldInfo).ToArray();
        }

        private FieldInfo CreateFieldInfo(PropertyInfo p)
        {
            var rawType = p.PropertyType.GetRawType();
            return new FieldInfo(rawType, p.Name);
        }
    }
}
