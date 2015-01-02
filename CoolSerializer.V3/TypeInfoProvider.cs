using System;
using System.Linq;
using System.Reflection;

namespace CoolSerializer.V3
{
    public class TypeInfoProvider
    {
        public TypeInfo Provide(Type type)
        {
            return new TypeInfo(Guid.NewGuid(), type.FullName, ProvideFields(type), type.IsValueType);
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
