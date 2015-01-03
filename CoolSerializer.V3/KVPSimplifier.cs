using System.Collections.Generic;

namespace CoolSerializer.V3
{
    public class KVPSimplifier<TValue, TKey> : ISimpifier<KeyValuePair<TKey, TValue>, SimpleKeyValuePair<TKey, TValue>>
    {
        public SimpleKeyValuePair<TKey, TValue> Simplify(KeyValuePair<TKey, TValue> obj)
        {
            return new SimpleKeyValuePair<TKey, TValue>() { Key = obj.Key, Value = obj.Value };
        }

        public KeyValuePair<TKey, TValue> Desimplify(SimpleKeyValuePair<TKey, TValue> simpleObj)
        {
            return new KeyValuePair<TKey, TValue>(simpleObj.Key, simpleObj.Value);
        }
    }

    public struct SimpleKeyValuePair<TKey, TValue>
    {
        public TKey Key { get; set; }
        public TValue Value { get; set; }
    }
}