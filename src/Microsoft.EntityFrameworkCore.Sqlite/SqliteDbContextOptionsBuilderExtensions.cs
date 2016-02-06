// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Common;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore
{
    public static class SqliteDbContextOptionsBuilderExtensions
    {
        public static SqliteDbContextOptionsBuilder UseSqlite([NotNull] this DbContextOptionsBuilder options, [NotNull] string connectionString)
        {
            Check.NotNull(options, nameof(options));
            Check.NotEmpty(connectionString, nameof(connectionString));

            var extension = GetOrCreateExtension(options);
            extension.ConnectionString = connectionString;
            ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(extension);

            return new SqliteDbContextOptionsBuilder(options);
        }

        public static SqliteDbContextOptionsBuilder UseSqlite([NotNull] this DbContextOptionsBuilder options, [NotNull] DbConnection connection)
        {
            Check.NotNull(options, nameof(options));
            Check.NotNull(connection, nameof(connection));

            var extension = GetOrCreateExtension(options);
            extension.Connection = connection;
            ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(extension);

            return new SqliteDbContextOptionsBuilder(options);
        }

        private static SqliteOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder options)
        {
            var existingExtension = options.Options.FindExtension<SqliteOptionsExtension>();

            return existingExtension != null
                ? new SqliteOptionsExtension(existingExtension)
                : new SqliteOptionsExtension();
        }
    }
}
