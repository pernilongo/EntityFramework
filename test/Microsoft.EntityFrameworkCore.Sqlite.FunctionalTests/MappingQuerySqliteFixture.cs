// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.FunctionalTests;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Sqlite.FunctionalTests.TestModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Sqlite.FunctionalTests
{
    public class MappingQuerySqliteFixture : MappingQueryFixtureBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly DbContextOptions _options;
        private readonly SqliteTestStore _testDatabase;

        public MappingQuerySqliteFixture()
        {
            _serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddSqlite()
                .ServiceCollection()
                .AddSingleton<ILoggerFactory>(new TestSqlLoggerFactory())
                .BuildServiceProvider();

            _testDatabase = SqliteNorthwindContext.GetSharedStore();

            var optionsBuilder = new DbContextOptionsBuilder().UseModel(CreateModel());
            optionsBuilder.UseSqlite(_testDatabase.ConnectionString)
                .SuppressForeignKeyEnforcement();
            _options = optionsBuilder.Options;
        }

        public DbContext CreateContext()
        {
            var context = new DbContext(_serviceProvider, _options);

            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            return context;
        }

        public void Dispose() => _testDatabase.Dispose();

        protected override string DatabaseSchema { get; } = null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MappingQueryTestBase.MappedCustomer>(e =>
                {
                    // TODO: Use .Sqlite() when available
                    e.Property(c => c.CompanyName2).Metadata.Relational().ColumnName = "CompanyName";
                    e.Metadata.Relational().TableName = "Customers";
                });
        }
    }
}
