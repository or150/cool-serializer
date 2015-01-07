using System.Collections.Generic;
using System.Linq.Expressions;

namespace CoolSerializer.V3
{
    public abstract class MutationHelper
    {
        protected MutationHelper(Expression graph)
        {
            Graph = graph;
            Variables = new List<ParameterExpression>();
            MethodBody = new List<Expression>();
            ExtraData = new Dictionary<string, object>();
        }
        public Expression Graph { get; set; }
        public List<ParameterExpression> Variables { get; set; }
        public List<Expression> MethodBody { get; set; }

        public Dictionary<string, object> ExtraData { get; private set; }
    }
    public class SerializationMutationHelper : MutationHelper
    {
        public SerializationMutationHelper(Serializer serializer, Expression graph, Expression writer)
            : base(graph)
        {
            Serializer = serializer;
            Writer = writer;
        }

        public Serializer Serializer { get; private set; }
        public Expression Writer { get; private set; }
    }

    public class DeserializationMutationHelper : MutationHelper
    {
        public DeserializationMutationHelper(Deserializer deserializer, Expression graph, Expression reader)
            : base(graph)
        {
            Deserializer = deserializer;
            Reader = reader;
        }

        public Deserializer Deserializer { get; private set; }
        public Expression Reader { get; private set; }
    }
}