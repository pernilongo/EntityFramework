// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Remotion.Linq.Clauses;
using Remotion.Linq.Parsing.Structure.IntermediateModel;

namespace Microsoft.Data.Entity.Query.ResultOperators.Internal
{
    public class FromSqlExpressionNode : ResultOperatorExpressionNodeBase
    {
        public static readonly IReadOnlyCollection<MethodInfo> SupportedMethods = new[]
        {
            RelationalQueryableExtensions.FromSqlMethodInfo
        };

        private readonly string _sql;
        private readonly Expression _arguments;

        public FromSqlExpressionNode(
            MethodCallExpressionParseInfo parseInfo,
            [NotNull] ConstantExpression sql,
            [NotNull] Expression arguments)
            : base(parseInfo, null, null)
        {
            _sql = (string)sql.Value;
            _arguments = arguments;
        }

        protected override ResultOperatorBase CreateResultOperator(ClauseGenerationContext clauseGenerationContext)
            => new FromSqlResultOperator(_sql, _arguments);

        public override Expression Resolve(
            ParameterExpression inputParameter,
            Expression expressionToBeResolved,
            ClauseGenerationContext clauseGenerationContext)
            => Source.Resolve(inputParameter, expressionToBeResolved, clauseGenerationContext);
    }
}
