// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Query.Sql;
using Microsoft.EntityFrameworkCore.Utilities;
using Remotion.Linq.Clauses;

namespace Microsoft.EntityFrameworkCore.Query.Expressions
{
    public class TableExpression : TableExpressionBase
    {
        public TableExpression(
            [NotNull] string table,
            [CanBeNull] string schema,
            [NotNull] string alias,
            [NotNull] IQuerySource querySource)
            : base(
                Check.NotNull(querySource, nameof(querySource)),
                Check.NotEmpty(alias, nameof(alias)))
        {
            Check.NotEmpty(table, nameof(table));

            Table = table;
            Schema = schema;
        }

        public virtual string Table { get; }

        public virtual string Schema { get; }

        protected override Expression Accept(ExpressionVisitor visitor)
        {
            Check.NotNull(visitor, nameof(visitor));

            var specificVisitor = visitor as ISqlExpressionVisitor;

            return specificVisitor != null
                ? specificVisitor.VisitTable(this)
                : base.Accept(visitor);
        }

        public override string ToString() => Table + " " + Alias;
    }
}
