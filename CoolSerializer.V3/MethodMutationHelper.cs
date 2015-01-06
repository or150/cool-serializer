using System.Collections.Generic;
using System.Linq.Expressions;

namespace CoolSerializer.V3
{
    public class MethodMutationHelper<T>
    {
        public MethodMutationHelper(T caller, Expression graph, Expression writer)
        {
            Caller = caller;
            Writer = writer;
            Graph = graph;
            Parameters = new List<ParameterExpression>();
            MethodBody = new List<Expression>();
        }

        public T Caller { get; private set; }
        public Expression Writer { get; private set; }
        public Expression Graph { get; set; }
        public List<ParameterExpression> Parameters { get; set; }
        public List<Expression> MethodBody { get; set; }
    }
}