// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.FunctionalTests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Microsoft.EntityFrameworkCore.Sqlite.FunctionalTests.TestModels
{
    public class SqliteNorthwindContext : NorthwindContext
    {
        public SqliteNorthwindContext(IServiceProvider serviceProvider, DbContextOptions options)
            : base(serviceProvider, options)
        {
        }

        public static SqliteTestStore GetSharedStore() => SqliteTestStore.GetOrCreateShared("northwind", () => { });
    }
}
