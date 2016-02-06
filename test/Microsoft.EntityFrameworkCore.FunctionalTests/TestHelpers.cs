// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class TestHelpers
    {
        protected TestHelpers()
        {
        }

        public static TestHelpers Instance { get; } = new TestHelpers();

        public DbContextOptions CreateOptions(IModel model)
        {
            var optionsBuilder = new DbContextOptionsBuilder();
            UseProviderOptions(optionsBuilder.UseModel(model));

            return optionsBuilder.Options;
        }

        public DbContextOptions CreateOptions()
        {
            var optionsBuilder = new DbContextOptionsBuilder();
            UseProviderOptions(optionsBuilder);

            return optionsBuilder.Options;
        }

        public IServiceProvider CreateServiceProvider(IServiceCollection customServices = null)
            => CreateServiceProvider(customServices, AddProviderServices);

        private IServiceProvider CreateServiceProvider(
            IServiceCollection customServices,
            Func<EntityFrameworkServicesBuilder, EntityFrameworkServicesBuilder> addProviderServices)
        {
            var services = new ServiceCollection();
            addProviderServices(services.AddEntityFramework());

            if (customServices != null)
            {
                foreach (var service in customServices)
                {
                    services.Add(service);
                }
            }

            return services.BuildServiceProvider();
        }

        public virtual EntityFrameworkServicesBuilder AddProviderServices(EntityFrameworkServicesBuilder builder) => builder.AddInMemoryDatabase();

        protected virtual void UseProviderOptions(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseInMemoryDatabase();

        public DbContext CreateContext(IServiceProvider serviceProvider, IModel model)
            => new DbContext(serviceProvider, CreateOptions(model));

        public DbContext CreateContext(IServiceProvider serviceProvider, DbContextOptions options)
            => new DbContext(serviceProvider, options);

        public DbContext CreateContext(IServiceProvider serviceProvider) => new DbContext(serviceProvider, CreateOptions());

        public DbContext CreateContext(IModel model) => new DbContext(CreateServiceProvider(), CreateOptions(model));

        public DbContext CreateContext(DbContextOptions options) => new DbContext(CreateServiceProvider(), options);

        public DbContext CreateContext() => new DbContext(CreateServiceProvider(), CreateOptions());

        public DbContext CreateContext(IServiceCollection customServices, IModel model)
            => new DbContext(CreateServiceProvider(customServices), CreateOptions(model));

        public DbContext CreateContext(IServiceCollection customServices, DbContextOptions options)
            => new DbContext(CreateServiceProvider(customServices), options);

        public DbContext CreateContext(IServiceCollection customServices)
            => new DbContext(CreateServiceProvider(customServices), CreateOptions());

        public IServiceProvider CreateContextServices(IServiceProvider serviceProvider, IModel model)
            => ((IInfrastructure<IServiceProvider>)CreateContext(serviceProvider, model)).Instance;

        public IServiceProvider CreateContextServices(IServiceProvider serviceProvider, DbContextOptions options)
            => ((IInfrastructure<IServiceProvider>)CreateContext(serviceProvider, options)).Instance;

        public IServiceProvider CreateContextServices(IServiceProvider serviceProvider) => ((IInfrastructure<IServiceProvider>)CreateContext(serviceProvider)).Instance;

        public IServiceProvider CreateContextServices(IModel model)
            => ((IInfrastructure<IServiceProvider>)CreateContext(model)).Instance;

        public IServiceProvider CreateContextServices(DbContextOptions options)
            => ((IInfrastructure<IServiceProvider>)CreateContext(options)).Instance;

        public IServiceProvider CreateContextServices()
            => ((IInfrastructure<IServiceProvider>)CreateContext()).Instance;

        public IServiceProvider CreateContextServices(IServiceCollection customServices, IModel model)
            => ((IInfrastructure<IServiceProvider>)CreateContext(customServices, model)).Instance;

        public IServiceProvider CreateContextServices(IServiceCollection customServices, DbContextOptions options)
            => ((IInfrastructure<IServiceProvider>)CreateContext(customServices, options)).Instance;

        public IServiceProvider CreateContextServices(IServiceCollection customServices)
            => ((IInfrastructure<IServiceProvider>)CreateContext(customServices)).Instance;

        public IMutableModel BuildModelFor<TEntity>() where TEntity : class
        {
            var builder = CreateConventionBuilder();
            builder.Entity<TEntity>();
            return builder.Model;
        }

        public ModelBuilder CreateConventionBuilder()
        {
            var contextServices = CreateContextServices();

            var conventionSetBuilder = contextServices.GetRequiredService<IDatabaseProviderServices>().ConventionSetBuilder;
            var conventionSet = contextServices.GetRequiredService<ICoreConventionSetBuilder>().CreateConventionSet();
            conventionSet = conventionSetBuilder == null
                ? conventionSet
                : conventionSetBuilder.AddConventions(conventionSet);
            return new ModelBuilder(conventionSet);
        }

        public InternalEntityEntry CreateInternalEntry<TEntity>(
            IModel model, EntityState entityState = EntityState.Detached, TEntity entity = null)
            where TEntity : class, new()
        {
            var entry = CreateContextServices(model)
                .GetRequiredService<IStateManager>()
                .GetOrCreateEntry(entity ?? new TEntity());

            entry.SetEntityState(entityState);

            return entry;
        }

        public static int AssertResults<T>(
            IList<T> expected,
            IList<T> actual,
            bool assertOrder,
            Action<IList<T>, IList<T>> asserter = null)
        {
            Assert.Equal(expected.Count, actual.Count);

            if (asserter != null)
            {
                asserter(expected, actual);
            }
            else
            {
                if (assertOrder)
                {
                    Assert.Equal(expected, actual);
                }
                else
                {
                    foreach (var expectedItem in expected)
                    {
                        Assert.True(
                            actual.Contains(expectedItem),
                            $"\r\nExpected item: [{expectedItem}] not found in results: [{string.Join(", ", actual.Take(10))}]...");
                    }
                }
            }
            return actual.Count;
        }
    }
}
