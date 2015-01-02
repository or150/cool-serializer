using System;
using System.Linq;
using System.Reflection;

namespace CoolSerializer.V3
{
    public class TypeInfoProvider
    {
        public TypeInfo Provide(Type type)
        {
            return new TypeInfo(Guid.NewGuid(), type.FullName, ProvideFields(type));
        }

        private FieldInfo[] ProvideFields(Type type)
        {
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => new FieldInfo(p.PropertyType.GetRawType(), p.Name)).ToArray();
        }
    }
}
