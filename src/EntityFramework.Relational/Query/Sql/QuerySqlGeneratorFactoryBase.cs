﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Query.Expressions;
using Microsoft.Data.Entity.Query.Sql.Internal;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Query.Sql
{
    public abstract class QuerySqlGeneratorFactoryBase : IQuerySqlGeneratorFactory
    {
        protected QuerySqlGeneratorFactoryBase(
            [NotNull] IRelationalCommandBuilderFactory commandBuilderFactory,
            [NotNull] ISqlGenerationHelper sqlGenerationHelper,
            [NotNull] IParameterNameGeneratorFactory parameterNameGeneratorFactory)
        {
            Check.NotNull(commandBuilderFactory, nameof(commandBuilderFactory));
            Check.NotNull(sqlGenerationHelper, nameof(sqlGenerationHelper));
            Check.NotNull(parameterNameGeneratorFactory, nameof(parameterNameGeneratorFactory));

            CommandBuilderFactory = commandBuilderFactory;
            SqlGenerationHelper = sqlGenerationHelper;
            ParameterNameGeneratorFactory = parameterNameGeneratorFactory;
        }

        protected virtual IRelationalCommandBuilderFactory CommandBuilderFactory { get; }
        protected virtual ISqlGenerationHelper SqlGenerationHelper { get; }
        protected virtual IParameterNameGeneratorFactory ParameterNameGeneratorFactory { get; }

        public abstract IQuerySqlGenerator CreateDefault(SelectExpression selectExpression);

        public virtual IQuerySqlGenerator CreateFromSql(
            SelectExpression selectExpression,
            string sql,
            string argumentsParameterName)
            => new FromSqlNonComposedQuerySqlGenerator(
                CommandBuilderFactory,
                SqlGenerationHelper,
                ParameterNameGeneratorFactory,
                Check.NotNull(selectExpression, nameof(selectExpression)),
                Check.NotEmpty(sql, nameof(sql)),
                Check.NotEmpty(argumentsParameterName, nameof(argumentsParameterName)));
    }
}
