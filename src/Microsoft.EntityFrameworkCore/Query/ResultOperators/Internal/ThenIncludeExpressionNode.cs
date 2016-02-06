// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Parsing.Structure.IntermediateModel;

namespace Microsoft.EntityFrameworkCore.Query.ResultOperators.Internal
{
    public class ThenIncludeExpressionNode : ResultOperatorExpressionNodeBase
    {
        public static readonly IReadOnlyCollection<MethodInfo> SupportedMethods = new[]
        {
            EntityFrameworkQueryableExtensions.ThenIncludeAfterCollectionMethodInfo,
            EntityFrameworkQueryableExtensions.ThenIncludeAfterReferenceMethodInfo
        };

        private readonly LambdaExpression _navigationPropertyPathLambda;

        public ThenIncludeExpressionNode(
            MethodCallExpressionParseInfo parseInfo,
            [NotNull] LambdaExpression navigationPropertyPathLambda)
            : base(parseInfo, null, null)
        {
            _navigationPropertyPathLambda = navigationPropertyPathLambda;
        }

        protected override void ApplyNodeSpecificSemantics(
            QueryModel queryModel, ClauseGenerationContext clauseGenerationContext)
        {
            var includeResultOperator
                = (IncludeResultOperator)clauseGenerationContext.GetContextInfo(Source);

            includeResultOperator
                .AppendToNavigationPath(_navigationPropertyPathLambda.GetComplexPropertyAccess());

            clauseGenerationContext.AddContextInfo(this, includeResultOperator);
        }

        protected override ResultOperatorBase CreateResultOperator(ClauseGenerationContext clauseGenerationContext)
            => null;

        public override Expression Resolve(
            ParameterExpression inputParameter,
            Expression expressionToBeResolved,
            ClauseGenerationContext clauseGenerationContext)
            => Source.Resolve(
                inputParameter,
                expressionToBeResolved,
                clauseGenerationContext);
    }
}
