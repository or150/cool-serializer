using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace CoolSerializer.V3
{
    public class UnknownObjectTypeInfoProvider : IBoundTypeInfoProvider
    {
        private readonly IBoundFieldInfoProvider mFieldsProvider;

        public UnknownObjectTypeInfoProvider(IBoundFieldInfoProvider fieldsProvider)
        {
            mFieldsProvider = fieldsProvider;
        }

        public bool TryProvide<T>(TypeInfo info, out IBoundTypeInfo boundTypeInfo)
        {
            var knownType = Type.GetType(info.Name);
            if (knownType != null)
            {
                boundTypeInfo = null;
                return false;
            }

            if (info.RawType == FieldType.Object)
            {
                if (typeof(T).IsExtraDataHolder() && !typeof(T).IsAbstract && !typeof(T).IsInterface)
                {
                    boundTypeInfo = new BoundTypeInfo(info, typeof(T), mFieldsProvider.Provide(info, typeof(T)));
                    return true;
                }

                var type = CreateType(typeof(T));
                boundTypeInfo = new BoundTypeInfo(info, type, mFieldsProvider.Provide(info, type));
                return true;
            }
            else if (info.RawType == FieldType.Collection)
            {
                //if (typeof (T).GetRawType() == FieldType.Collection && typeof(T).IsExtraDataHolder() && !typeof (T).IsAbstract && !typeof (T).IsInterface)
                //{
                //    boundTypeInfo = new BoundCollectionTypeInfo(info, typeof(T));
                //    return true;
                //}
                //return new BoundCollectionTypeInfo(info,typeof(List<object>))
            }
            boundTypeInfo = null;
            return false;
        }

        static ConcurrentDictionary<Type, Type> DynamicTypes = new ConcurrentDictionary<Type, Type>();

        private Type CreateType(Type type)
        {
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("Generated" + Guid.NewGuid().ToString("N")),
                AssemblyBuilderAccess.RunAndSave);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("Module", "asdasd.dll", true);

            var debuggableCtor = typeof(DebuggableAttribute).GetConstructor(new[] { typeof(DebuggableAttribute.DebuggingModes) });
            var x = new CustomAttributeBuilder(debuggableCtor,
                new object[] { DebuggableAttribute.DebuggingModes.Default });
            assemblyBuilder.SetCustomAttribute(x);
            moduleBuilder.SetCustomAttribute(x);


            var typeBuilder = moduleBuilder.DefineType(type.Name + "-" + Guid.NewGuid().ToString("N"), TypeAttributes.Public | TypeAttributes.Class, type.IsClass ? type : null);
            typeBuilder.AddInterfaceImplementation(typeof(IExtraDataHolder));
            foreach (var property in typeof(IExtraDataHolder).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                CreateProperty(typeBuilder, property, true);
            }
            if (type.IsAbstract)
            {
                //type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(m=>m.IsAbstract)
                var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(p => p.CanWrite && p.GetGetMethod(true).IsAbstract && p.GetSetMethod(true).IsAbstract);
                foreach (var prop in props)
                {
                    CreateProperty(typeBuilder, prop, false);
                }
            }

            var retType = typeBuilder.CreateType();
            return retType;
        }

        private void CreateProperty(TypeBuilder builder, PropertyInfo property, bool isExplicit)
        {
            var name = isExplicit ? "A" + Guid.NewGuid().ToString("N") : property.Name;
            var field = builder.DefineField(name + "_field", property.PropertyType, FieldAttributes.Private);
            var prop = builder.DefineProperty(name, PropertyAttributes.None, property.PropertyType, Type.EmptyTypes);

            var getterMethod = builder.DefineMethod("get_" + name, isExplicit ? ExplicitImplementation : ImplicitImplementation, property.PropertyType, Type.EmptyTypes);
            var getGenerator = getterMethod.GetILGenerator();
            getGenerator.Emit(OpCodes.Ldarg_0);
            getGenerator.Emit(OpCodes.Ldfld, field);
            getGenerator.Emit(OpCodes.Ret);

            var setterMethod = builder.DefineMethod("set_" + name, isExplicit ? ExplicitImplementation : ImplicitImplementation, null, new[] { property.PropertyType });
            var setGenerator = setterMethod.GetILGenerator();
            setGenerator.Emit(OpCodes.Ldarg_0);
            setGenerator.Emit(OpCodes.Ldarg_1);
            setGenerator.Emit(OpCodes.Stfld, field);
            setGenerator.Emit(OpCodes.Ret);

            builder.DefineMethodOverride(getterMethod, property.GetGetMethod(true));
            builder.DefineMethodOverride(setterMethod, property.GetSetMethod(true));
            prop.SetGetMethod(getterMethod);
            prop.SetSetMethod(setterMethod);
        }

        private const MethodAttributes ExplicitImplementation = MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final;

        private const MethodAttributes ImplicitImplementation = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig;
    }
}