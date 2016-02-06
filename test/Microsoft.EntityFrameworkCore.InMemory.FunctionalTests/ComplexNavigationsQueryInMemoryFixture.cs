// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.FunctionalTests;
using Microsoft.EntityFrameworkCore.FunctionalTests.TestModels.ComplexNavigationsModel;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.EntityFrameworkCore.InMemory.FunctionalTests
{
    public class ComplexNavigationsQueryInMemoryFixture : ComplexNavigationsQueryFixtureBase<InMemoryTestStore>
    {
        public const string DatabaseName = "InMemoryQueryTest";

        private readonly IServiceProvider _serviceProvider;
        private readonly DbContextOptions _options;

        public ComplexNavigationsQueryInMemoryFixture()
        {
            _serviceProvider
                = new ServiceCollection()
                    .AddEntityFramework()
                    .AddInMemoryDatabase()
                    .ServiceCollection()
                    .AddSingleton(TestInMemoryModelSource.GetFactory(OnModelCreating))
                    .BuildServiceProvider();

            var optionsBuilder = new DbContextOptionsBuilder();

            optionsBuilder.UseInMemoryDatabase();

            _options = optionsBuilder.Options;
        }

        public override InMemoryTestStore CreateTestStore()
        {
            return InMemoryTestStore.GetOrCreateShared(DatabaseName, () =>
            {
                using (var context = new ComplexNavigationsContext(_serviceProvider, _options))
                {
                    ComplexNavigationsModelInitializer.Seed(context);
                }
            });
        }

        public override ComplexNavigationsContext CreateContext(InMemoryTestStore _)
        {
            var context = new ComplexNavigationsContext(_serviceProvider, _options);

            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            return context;
        }
    }
}
