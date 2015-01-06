using System;
using System.Collections.Generic;
using System.Linq;

namespace CoolSerializer.V3
{
    public interface ISimplifersProvider
    {
        bool TryProvide(Type realType, out object simplifer);
        bool TryGetSimplifiedType(Type realType, out Type simplifiedType);
    }

    class BasicSimplifersProvider : ISimplifersProvider
    {
        private readonly Dictionary<Type,Type> mSimplifiers = new Dictionary<Type, Type>();

        public BasicSimplifersProvider()
        {
            mSimplifiers.Add(typeof(KeyValuePair<,>),typeof(KVPSimplifier<,>));
            mSimplifiers.Add(typeof(MyBadClass),typeof(MyNiceSimplifier));
        }
        public bool TryProvide(Type realType, out object simplifer)
        {
            Type simplifierType;
            if (TryProvideType(realType, out simplifierType))
            {
                simplifer = Activator.CreateInstance(simplifierType);
                return true;
            }
            simplifer = null;
            return false;
        }

        private bool TryProvideType(Type realType, out Type simplifierType)
        {
            if (mSimplifiers.TryGetValue(realType, out simplifierType))
            {
                return true;
            }

            if (realType.IsGenericType)
            {
                var genericType = realType.GetGenericTypeDefinition();
                Type genericSimplifierType;
                if (mSimplifiers.TryGetValue(genericType, out genericSimplifierType))
                {
                    var realTypeWithGenericArgs = SimplifiersHelper.GetRealType(genericSimplifierType);

                    var genericArguments = realTypeWithGenericArgs.GetGenericArguments();
                    var arguments = realType.GetGenericArguments();
                    var argsMap = genericArguments.Zip(arguments, Tuple.Create).ToDictionary(x => x.Item1, x => x.Item2);

                    simplifierType = genericSimplifierType.MakeGenericType(genericSimplifierType.GetGenericArguments().Select(x => argsMap[x]).ToArray());
                    return true;
                }
            }
            return false;
        }

        public bool TryGetSimplifiedType(Type realType, out Type simplifiedType)
        {
            Type simplifierType;
            if (TryProvideType(realType, out simplifierType))
            {
                simplifiedType = SimplifiersHelper.GetSimplifiedType(simplifierType);
                return true;
            }
            simplifiedType = null;
            return false;
        }
    }

    public interface ISimpifier<T, TSimple>
    {
        TSimple Simplify(T obj);
        T Desimplify(TSimple simpleObj);
    }
}