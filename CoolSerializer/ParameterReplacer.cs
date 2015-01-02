using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace CoolSerializer
{
    class ParameterReplacer : ExpressionVisitor
    {
        private readonly List<Expression> mOriginalParam;
        private readonly List<Expression> mNewParam;

        public ParameterReplacer(IEnumerable<Expression> originalParams, IEnumerable<Expression> newParams)
        {
            mOriginalParam = originalParams as List<Expression> ?? originalParams.ToList();
            mNewParam = newParams as List<Expression> ?? newParams.ToList();
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            var idx = mOriginalParam.IndexOf(node);
            if (idx != -1)
            {
                return base.Visit(mNewParam[idx]);
            }
            return base.VisitParameter(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var idx = mOriginalParam.IndexOf(node);
            if (idx != -1)
            {
                return base.Visit(mNewParam[idx]);
            }
            return base.VisitMember(node);
        }
    }
}