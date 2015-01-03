using System;
using System.Linq;
using System.Reflection;

namespace CoolSerializer.V3
{
    public class TypeInfoProvider
    {
        static class TypeInfoHelper<T>
        {
            private static FieldType mRawType;
            private static bool mIsClass;
            private static Type mType;

            static TypeInfoHelper()
            {
                mRawType = typeof (T).GetRawType();
                mIsClass = !typeof (T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;
                mType = Nullable.GetUnderlyingType(typeof (T)) ?? typeof (T);
            }
            public static FieldType ProvideRawType(T graph)
            {
                return mIsClass && graph != null ? graph.GetType().GetRawType() : mRawType;
            }

            public static Type ProvideType(T graph)
            {
                return mIsClass && graph != null ? graph.GetType() : mType;
            }
        }
        public TypeInfo Provide<T>(T graph)
        {
            var type = TypeInfoHelper<T>.ProvideType(graph);
            return new TypeInfo(Guid.NewGuid(), type.FullName, ProvideFields(type), type.IsValueType);
        }

        public FieldType ProvideRawType<T>(T graph)
        {
            return TypeInfoHelper<T>.ProvideRawType(graph);
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
