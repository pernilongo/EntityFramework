// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.ExpressionTranslators;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionVisitors
{
    public class SqlTranslatingExpressionVisitorFactory : ISqlTranslatingExpressionVisitorFactory
    {
        private readonly IRelationalAnnotationProvider _relationalAnnotationProvider;
        private readonly IExpressionFragmentTranslator _compositeExpressionFragmentTranslator;
        private readonly IMethodCallTranslator _methodCallTranslator;
        private readonly IMemberTranslator _memberTranslator;

        public SqlTranslatingExpressionVisitorFactory(
            [NotNull] IRelationalAnnotationProvider relationalAnnotationProvider,
            [NotNull] IExpressionFragmentTranslator compositeExpressionFragmentTranslator,
            [NotNull] IMethodCallTranslator methodCallTranslator,
            [NotNull] IMemberTranslator memberTranslator)
        {
            Check.NotNull(relationalAnnotationProvider, nameof(relationalAnnotationProvider));
            Check.NotNull(compositeExpressionFragmentTranslator, nameof(compositeExpressionFragmentTranslator));
            Check.NotNull(methodCallTranslator, nameof(methodCallTranslator));
            Check.NotNull(memberTranslator, nameof(memberTranslator));

            _relationalAnnotationProvider = relationalAnnotationProvider;
            _compositeExpressionFragmentTranslator = compositeExpressionFragmentTranslator;
            _methodCallTranslator = methodCallTranslator;
            _memberTranslator = memberTranslator;
        }

        public virtual SqlTranslatingExpressionVisitor Create(
            [NotNull] RelationalQueryModelVisitor queryModelVisitor,
            [CanBeNull] SelectExpression targetSelectExpression = null,
            [CanBeNull] Expression topLevelPredicate = null,
            bool bindParentQueries = false,
            bool inProjection = false)
            => new SqlTranslatingExpressionVisitor(
                _relationalAnnotationProvider,
                _compositeExpressionFragmentTranslator,
                _methodCallTranslator,
                _memberTranslator,
                Check.NotNull(queryModelVisitor, nameof(queryModelVisitor)),
                targetSelectExpression,
                topLevelPredicate,
                bindParentQueries,
                inProjection);
    }
}
