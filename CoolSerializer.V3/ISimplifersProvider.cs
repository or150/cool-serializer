using System;
using System.Collections.Generic;
using System.Linq;

namespace CoolSerializer.V3
{
    public interface ISimplifersProvider
    {
        bool TryProvide(Type realType, out object simplifer);
    }

    class BasicSimplifersProvider : ISimplifersProvider
    {
        private Dictionary<Type,Type> mSimplifiers = new Dictionary<Type, Type>();

        public BasicSimplifersProvider()
        {
            mSimplifiers.Add(typeof(KeyValuePair<,>),typeof(KVPSimplifier<,>));
            mSimplifiers.Add(typeof(MyBadClass),typeof(MyNiceSimplifier));
        }
        public bool TryProvide(Type realType, out object simplifer)
        {
            Type simpliferType;
            if (mSimplifiers.TryGetValue(realType, out simpliferType))
            {
                simplifer = Activator.CreateInstance(simpliferType);
                return true;
            }

            if (realType.IsGenericType)
            {
                var genericType = realType.GetGenericTypeDefinition();
                if (mSimplifiers.TryGetValue(genericType, out simpliferType))
                {
                    var simpliferInterface = GetSimpliferInterface(simpliferType);
                    var realTypeWithGenericArgs = GetRealType(simpliferInterface);
                    
                    var genericArguments = realTypeWithGenericArgs.GetGenericArguments();
                    var arguments = realType.GetGenericArguments();
                    var argsMap = genericArguments.Zip(arguments, Tuple.Create).ToDictionary(x => x.Item1, x => x.Item2);

                    var type = simpliferType.MakeGenericType(simpliferType.GetGenericArguments().Select(x => argsMap[x]).ToArray());
                    simplifer = Activator.CreateInstance(type);
                    return true;
                }
            }
            simplifer = null;
            return false;
        }

        private static Type GetRealType(Type simpliferInterface)
        {
            return simpliferInterface.GetGenericArguments()[0];
        }

        private static Type GetSimpliferInterface(Type simpliferType)
        {
            return simpliferType.GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof (ISimpifier<,>));
        }
    }

    public interface ISimpifier<T, TSimple>
    {
        TSimple Simplify(T obj);
        T Desimplify(TSimple simpleObj);
    }
}