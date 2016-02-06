// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class DbContextTest
    {
        [Fact]
        public void Set_throws_for_type_not_in_model()
        {
            var optionsBuilder = new DbContextOptionsBuilder();
            optionsBuilder.UseInMemoryDatabase();

            using (var context = new DbContext(TestHelpers.Instance.CreateServiceProvider(), optionsBuilder.Options))
            {
                var ex = Assert.Throws<InvalidOperationException>(() => context.Set<Category>());
                Assert.Equal(CoreStrings.InvalidSetType(nameof(Category)), ex.Message);
            }
        }

        [Fact]
        public void Each_context_gets_new_scoped_services()
        {
            var serviceProvider = TestHelpers.Instance.CreateServiceProvider();

            IServiceProvider contextServices;
            using (var context = new EarlyLearningCenter(serviceProvider))
            {
                contextServices = ((IInfrastructure<IServiceProvider>)context).Instance;
                Assert.Same(contextServices, ((IInfrastructure<IServiceProvider>)context).Instance);
            }

            using (var context = new EarlyLearningCenter(serviceProvider))
            {
                Assert.NotSame(contextServices, ((IInfrastructure<IServiceProvider>)context).Instance);
            }
        }

        [Fact]
        public void Each_context_gets_new_scoped_services_with_implicit_services()
        {
            IServiceProvider contextServices;
            using (var context = new Mock<DbContext> { CallBase = true }.Object)
            {
                contextServices = ((IInfrastructure<IServiceProvider>)context).Instance;
                Assert.Same(contextServices, ((IInfrastructure<IServiceProvider>)context).Instance);
            }

            using (var context = new Mock<DbContext> { CallBase = true }.Object)
            {
                Assert.NotSame(contextServices, ((IInfrastructure<IServiceProvider>)context).Instance);
            }
        }

        [Fact]
        public void Each_context_gets_new_scoped_services_with_explicit_config()
        {
            var serviceProvider = TestHelpers.Instance.CreateServiceProvider();

            var options = new DbContextOptionsBuilder().Options;

            IServiceProvider contextServices;
            using (var context = new DbContext(serviceProvider, options))
            {
                contextServices = ((IInfrastructure<IServiceProvider>)context).Instance;
                Assert.Same(contextServices, ((IInfrastructure<IServiceProvider>)context).Instance);
            }

            using (var context = new DbContext(serviceProvider, options))
            {
                Assert.NotSame(contextServices, ((IInfrastructure<IServiceProvider>)context).Instance);
            }
        }

        [Fact]
        public void Each_context_gets_new_scoped_services_with_implicit_services_and_explicit_config()
        {
            var options = new DbContextOptionsBuilder().Options;

            IServiceProvider contextServices;
            using (var context = new DbContext(options))
            {
                contextServices = ((IInfrastructure<IServiceProvider>)context).Instance;
                Assert.Same(contextServices, ((IInfrastructure<IServiceProvider>)context).Instance);
            }

            using (var context = new DbContext(options))
            {
                Assert.NotSame(contextServices, ((IInfrastructure<IServiceProvider>)context).Instance);
            }
        }

        [Fact]
        public void SaveChanges_calls_DetectChanges()
        {
            var services = new ServiceCollection()
                .AddScoped<IStateManager, FakeStateManager>()
                .AddScoped<IChangeDetector, FakeChangeDetector>();

            var serviceProvider = TestHelpers.Instance.CreateServiceProvider(services);

            using (var context = new DbContext(serviceProvider, new DbContextOptionsBuilder().Options))
            {
                var changeDetector = (FakeChangeDetector)context.GetService<IChangeDetector>();

                Assert.False(changeDetector.DetectChangesCalled);

                context.SaveChanges();

                Assert.True(changeDetector.DetectChangesCalled);
            }
        }

        [Fact]
        public void SaveChanges_calls_state_manager_SaveChanges()
        {
            var services = new ServiceCollection()
                .AddScoped<IStateManager, FakeStateManager>()
                .AddScoped<IChangeDetector, FakeChangeDetector>();

            var serviceProvider = TestHelpers.Instance.CreateServiceProvider(services);

            using (var context = new DbContext(serviceProvider, new DbContextOptionsBuilder().Options))
            {
                var stateManager = (FakeStateManager)context.GetService<IStateManager>();

                var entryMock = CreateInternalEntryMock();
                entryMock.Setup(m => m.EntityState).Returns(EntityState.Modified);
                stateManager.InternalEntries = new[] { entryMock.Object };

                Assert.False(stateManager.SaveChangesCalled);

                context.SaveChanges();

                Assert.True(stateManager.SaveChangesCalled);
            }
        }

        [Fact]
        public async Task SaveChangesAsync_calls_state_manager_SaveChangesAsync()
        {
            var services = new ServiceCollection()
                .AddScoped<IStateManager, FakeStateManager>()
                .AddScoped<IChangeDetector, FakeChangeDetector>();

            var serviceProvider = TestHelpers.Instance.CreateServiceProvider(services);

            using (var context = new DbContext(serviceProvider, new DbContextOptionsBuilder().Options))
            {
                context.ChangeTracker.AutoDetectChangesEnabled = false;

                var stateManager = (FakeStateManager)context.GetService<IStateManager>();

                var entryMock = CreateInternalEntryMock();
                entryMock.Setup(m => m.EntityState).Returns(EntityState.Modified);
                stateManager.InternalEntries = new[] { entryMock.Object };

                Assert.False(stateManager.SaveChangesAsyncCalled);

                await context.SaveChangesAsync();

                Assert.True(stateManager.SaveChangesAsyncCalled);
            }
        }

        [Fact]
        public void Entry_methods_check_arguments()
        {
            var services = new ServiceCollection()
                .AddScoped<IStateManager, FakeStateManager>();

            var serviceProvider = TestHelpers.Instance.CreateServiceProvider(services);

            using (var context = new EarlyLearningCenter(serviceProvider))
            {
                Assert.Equal(
                    "entity",
                    // ReSharper disable once AssignNullToNotNullAttribute
                    Assert.Throws<ArgumentNullException>(() => context.Entry(null)).ParamName);
                Assert.Equal(
                    "entity",
                    // ReSharper disable once AssignNullToNotNullAttribute
                    Assert.Throws<ArgumentNullException>(() => context.Entry<Random>(null)).ParamName);
            }
        }

        [Fact]
        public void Entry_methods_delegate_to_underlying_state_manager()
        {
            var entity = new Random();
            var stateManagerMock = new Mock<IStateManager>();
            var entry = CreateInternalEntryMock().Object;
            stateManagerMock.Setup(m => m.GetOrCreateEntry(entity)).Returns(entry);

            var services = new ServiceCollection()
                .AddScoped(_ => stateManagerMock.Object);

            var serviceProvider = TestHelpers.Instance.CreateServiceProvider(services);

            using (var context = new EarlyLearningCenter(serviceProvider))
            {
                Assert.Same(entry, context.Entry(entity).GetInfrastructure());
                Assert.Same(entry, context.Entry((object)entity).GetInfrastructure());
            }
        }

        private class FakeStateManager : IStateManager
        {
            public IEnumerable<InternalEntityEntry> InternalEntries { get; set; }
            public bool SaveChangesCalled { get; set; }
            public bool SaveChangesAsyncCalled { get; set; }
            public virtual bool? SingleQueryMode { get; set; }

            public void UpdateIdentityMap(InternalEntityEntry entry, IKey principalKey)
            {
                throw new NotImplementedException();
            }

            public void UpdateDependentMap(InternalEntityEntry entry, IForeignKey foreignKey)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<InternalEntityEntry> GetDependents(InternalEntityEntry principalEntry, IForeignKey foreignKey)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<InternalEntityEntry> GetDependentsUsingRelationshipSnapshot(InternalEntityEntry principalEntry, IForeignKey foreignKey)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<InternalEntityEntry> GetDependentsFromNavigation(InternalEntityEntry principalEntry, IForeignKey foreignKey)
            {
                throw new NotImplementedException();
            }

            public int SaveChanges(bool acceptAllChangesOnSuccess)
            {
                SaveChangesCalled = true;
                return 1;
            }

            public Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = new CancellationToken())
            {
                SaveChangesAsyncCalled = true;
                return Task.FromResult(1);
            }

            public virtual void AcceptAllChanges()
            {
                throw new NotImplementedException();
            }

            public InternalEntityEntry GetOrCreateEntry(object entity)
            {
                throw new NotImplementedException();
            }

            public InternalEntityEntry StartTrackingFromQuery(
                IEntityType entityType,
                object entity,
                ValueBuffer valueBuffer)
            {
                throw new NotImplementedException();
            }

            public void BeginTrackingQuery()
            {
                throw new NotImplementedException();
            }

            public InternalEntityEntry TryGetEntry(IKey key, ValueBuffer valueBuffer, bool throwOnNullKey)
            {
                throw new NotImplementedException();
            }

            public InternalEntityEntry TryGetEntry(object entity)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<InternalEntityEntry> Entries => Entries ?? Enumerable.Empty<InternalEntityEntry>();

            public IInternalEntityEntryNotifier Notify
            {
                get { throw new NotImplementedException(); }
            }

            public IValueGenerationManager ValueGeneration
            {
                get { throw new NotImplementedException(); }
            }

            public InternalEntityEntry StartTracking(InternalEntityEntry entry)
            {
                throw new NotImplementedException();
            }

            public void StopTracking(InternalEntityEntry entry)
            {
                throw new NotImplementedException();
            }

            public virtual void RecordDanglingDependent(IForeignKey foreignKey, InternalEntityEntry entry)
            {
                throw new NotImplementedException();
            }

            public virtual IEnumerable<InternalEntityEntry> GetDanglingDependents(IForeignKey foreignKey, InternalEntityEntry entry)
            {
                throw new NotImplementedException();
            }

            public InternalEntityEntry GetPrincipal(InternalEntityEntry entityEntry, IForeignKey foreignKey)
            {
                throw new NotImplementedException();
            }

            public InternalEntityEntry GetPrincipalUsingRelationshipSnapshot(InternalEntityEntry entityEntry, IForeignKey foreignKey)
            {
                throw new NotImplementedException();
            }

            public DbContext Context
            {
                get { throw new NotImplementedException(); }
            }
        }

        private class FakeChangeDetector : IChangeDetector
        {
            public bool DetectChangesCalled { get; set; }

            public void DetectChanges(IStateManager stateManager)
            {
                DetectChangesCalled = true;
            }

            public void DetectChanges(InternalEntityEntry entry)
            {
            }

            public void PropertyChanged(InternalEntityEntry entry, IPropertyBase property)
            {
            }

            public void PropertyChanging(InternalEntityEntry entry, IPropertyBase property)
            {
            }

            public virtual void Suspend()
            {
            }

            public virtual void Resume()
            {
            }
        }

        [Fact]
        public void Can_add_existing_entities_to_context_to_be_deleted()
        {
            TrackEntitiesTest((c, e) => c.Remove(e), (c, e) => c.Remove(e), EntityState.Deleted);
        }

        [Fact]
        public void Can_add_new_entities_to_context_with_graph_method()
        {
            TrackEntitiesTest((c, e) => c.Add(e), (c, e) => c.Add(e), EntityState.Added);
        }

        [Fact]
        public void Can_add_existing_entities_to_context_to_be_attached_with_graph_method()
        {
            TrackEntitiesTest((c, e) => c.Attach(e), (c, e) => c.Attach(e), EntityState.Unchanged);
        }

        [Fact]
        public void Can_add_existing_entities_to_context_to_be_updated_with_graph_method()
        {
            TrackEntitiesTest((c, e) => c.Update(e), (c, e) => c.Update(e), EntityState.Modified);
        }

        private static void TrackEntitiesTest(
            Func<DbContext, Category, EntityEntry<Category>> categoryAdder,
            Func<DbContext, Product, EntityEntry<Product>> productAdder, EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category1 = new Category { Id = 1, Name = "Beverages" };
                var category2 = new Category { Id = 2, Name = "Foods" };
                var product1 = new Product { Id = 1, Name = "Marmite", Price = 7.99m };
                var product2 = new Product { Id = 2, Name = "Bovril", Price = 4.99m };

                var categoryEntry1 = categoryAdder(context, category1);
                var categoryEntry2 = categoryAdder(context, category2);
                var productEntry1 = productAdder(context, product1);
                var productEntry2 = productAdder(context, product2);

                Assert.Same(category1, categoryEntry1.Entity);
                Assert.Same(category2, categoryEntry2.Entity);
                Assert.Same(product1, productEntry1.Entity);
                Assert.Same(product2, productEntry2.Entity);

                Assert.Same(category1, categoryEntry1.Entity);
                Assert.Equal(expectedState, categoryEntry1.State);
                Assert.Same(category2, categoryEntry2.Entity);
                Assert.Equal(expectedState, categoryEntry2.State);

                Assert.Same(product1, productEntry1.Entity);
                Assert.Equal(expectedState, productEntry1.State);
                Assert.Same(product2, productEntry2.Entity);
                Assert.Equal(expectedState, productEntry2.State);

                Assert.Same(categoryEntry1.GetInfrastructure(), context.Entry(category1).GetInfrastructure());
                Assert.Same(categoryEntry2.GetInfrastructure(), context.Entry(category2).GetInfrastructure());
                Assert.Same(productEntry1.GetInfrastructure(), context.Entry(product1).GetInfrastructure());
                Assert.Same(productEntry2.GetInfrastructure(), context.Entry(product2).GetInfrastructure());
            }
        }

        [Fact]
        public void Can_add_multiple_new_entities_to_context()
        {
            TrackMultipleEntitiesTest((c, e) => c.AddRange(e[0], e[1]), (c, e) => c.AddRange(e[0], e[1]), EntityState.Added);
        }

        [Fact]
        public void Can_add_multiple_existing_entities_to_context_to_be_attached()
        {
            TrackMultipleEntitiesTest((c, e) => c.AttachRange(e[0], e[1]), (c, e) => c.AttachRange(e[0], e[1]), EntityState.Unchanged);
        }

        [Fact]
        public void Can_add_multiple_existing_entities_to_context_to_be_updated()
        {
            TrackMultipleEntitiesTest((c, e) => c.UpdateRange(e[0], e[1]), (c, e) => c.UpdateRange(e[0], e[1]), EntityState.Modified);
        }

        [Fact]
        public void Can_add_multiple_existing_entities_to_context_to_be_deleted()
        {
            TrackMultipleEntitiesTest((c, e) => c.RemoveRange(e[0], e[1]), (c, e) => c.RemoveRange(e[0], e[1]), EntityState.Deleted);
        }

        private static void TrackMultipleEntitiesTest(
            Action<DbContext, object[]> categoryAdder,
            Action<DbContext, object[]> productAdder, EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category1 = new Category { Id = 1, Name = "Beverages" };
                var category2 = new Category { Id = 2, Name = "Foods" };
                var product1 = new Product { Id = 1, Name = "Marmite", Price = 7.99m };
                var product2 = new Product { Id = 2, Name = "Bovril", Price = 4.99m };

                categoryAdder(context, new[] { category1, category2 });
                productAdder(context, new[] { product1, product2 });

                Assert.Same(category1, context.Entry(category1).Entity);
                Assert.Same(category2, context.Entry(category2).Entity);
                Assert.Same(product1, context.Entry(product1).Entity);
                Assert.Same(product2, context.Entry(product2).Entity);

                Assert.Same(category1, context.Entry(category1).Entity);
                Assert.Equal(expectedState, context.Entry(category1).State);
                Assert.Same(category2, context.Entry(category2).Entity);
                Assert.Equal(expectedState, context.Entry(category2).State);

                Assert.Same(product1, context.Entry(product1).Entity);
                Assert.Equal(expectedState, context.Entry(product1).State);
                Assert.Same(product2, context.Entry(product2).Entity);
                Assert.Equal(expectedState, context.Entry(product2).State);
            }
        }

        [Fact]
        public void Can_add_existing_entities_with_default_value_to_context_to_be_deleted()
        {
            TrackEntitiesDefaultValueTest((c, e) => c.Remove(e), (c, e) => c.Remove(e), EntityState.Deleted);
        }

        [Fact]
        public void Can_add_new_entities_with_default_value_to_context_with_graph_method()
        {
            TrackEntitiesDefaultValueTest((c, e) => c.Add(e), (c, e) => c.Add(e), EntityState.Added);
        }

        [Fact]
        public void Can_add_existing_entities_with_default_value_to_context_to_be_attached_with_graph_method()
        {
            TrackEntitiesDefaultValueTest((c, e) => c.Attach(e), (c, e) => c.Attach(e), EntityState.Unchanged);
        }

        [Fact]
        public void Can_add_existing_entities_with_default_value_to_context_to_be_updated_with_graph_method()
        {
            TrackEntitiesDefaultValueTest((c, e) => c.Update(e), (c, e) => c.Update(e), EntityState.Modified);
        }

        // Issue #3890
        private static void TrackEntitiesDefaultValueTest(
            Func<DbContext, Category, EntityEntry<Category>> categoryAdder,
            Func<DbContext, Product, EntityEntry<Product>> productAdder, EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category1 = new Category { Id = 0, Name = "Beverages" };
                var product1 = new Product { Id = 0, Name = "Marmite", Price = 7.99m };

                var categoryEntry1 = categoryAdder(context, category1);
                var productEntry1 = productAdder(context, product1);

                Assert.Same(category1, categoryEntry1.Entity);
                Assert.Same(product1, productEntry1.Entity);

                Assert.Same(category1, categoryEntry1.Entity);
                Assert.Equal(expectedState, categoryEntry1.State);

                Assert.Same(product1, productEntry1.Entity);
                Assert.Equal(expectedState, productEntry1.State);

                Assert.Same(categoryEntry1.GetInfrastructure(), context.Entry(category1).GetInfrastructure());
                Assert.Same(productEntry1.GetInfrastructure(), context.Entry(product1).GetInfrastructure());
            }
        }

        [Fact]
        public void Can_add_multiple_new_entities_with_default_values_to_context()
        {
            TrackMultipleEntitiesDefaultValuesTest((c, e) => c.AddRange(e[0]), (c, e) => c.AddRange(e[0]), EntityState.Added);
        }

        [Fact]
        public void Can_add_multiple_existing_entities_with_default_values_to_context_to_be_attached()
        {
            TrackMultipleEntitiesDefaultValuesTest((c, e) => c.AttachRange(e[0]), (c, e) => c.AttachRange(e[0]), EntityState.Unchanged);
        }

        [Fact]
        public void Can_add_multiple_existing_entities_with_default_values_to_context_to_be_updated()
        {
            TrackMultipleEntitiesDefaultValuesTest((c, e) => c.UpdateRange(e[0]), (c, e) => c.UpdateRange(e[0]), EntityState.Modified);
        }

        [Fact]
        public void Can_add_multiple_existing_entities_with_default_values_to_context_to_be_deleted()
        {
            TrackMultipleEntitiesDefaultValuesTest((c, e) => c.RemoveRange(e[0]), (c, e) => c.RemoveRange(e[0]), EntityState.Deleted);
        }

        // Issue #3890
        private static void TrackMultipleEntitiesDefaultValuesTest(
            Action<DbContext, object[]> categoryAdder,
            Action<DbContext, object[]> productAdder, EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category1 = new Category { Id = 0, Name = "Beverages" };
                var product1 = new Product { Id = 0, Name = "Marmite", Price = 7.99m };

                categoryAdder(context, new[] { category1 });
                productAdder(context, new[] { product1 });

                Assert.Same(category1, context.Entry(category1).Entity);
                Assert.Same(product1, context.Entry(product1).Entity);

                Assert.Same(category1, context.Entry(category1).Entity);
                Assert.Equal(expectedState, context.Entry(category1).State);

                Assert.Same(product1, context.Entry(product1).Entity);
                Assert.Equal(expectedState, context.Entry(product1).State);
            }
        }

        [Fact]
        public void Can_add_no_new_entities_to_context()
        {
            TrackNoEntitiesTest(c => c.AddRange(), c => c.AddRange());
        }

        [Fact]
        public void Can_add_no_existing_entities_to_context_to_be_attached()
        {
            TrackNoEntitiesTest(c => c.AttachRange(), c => c.AttachRange());
        }

        [Fact]
        public void Can_add_no_existing_entities_to_context_to_be_updated()
        {
            TrackNoEntitiesTest(c => c.UpdateRange(), c => c.UpdateRange());
        }

        [Fact]
        public void Can_add_no_existing_entities_to_context_to_be_deleted()
        {
            TrackNoEntitiesTest(c => c.RemoveRange(), c => c.RemoveRange());
        }

        private static void TrackNoEntitiesTest(Action<DbContext> categoryAdder, Action<DbContext> productAdder)
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                categoryAdder(context);
                productAdder(context);
                Assert.Empty(context.ChangeTracker.Entries());
            }
        }

        [Fact]
        public void Can_add_existing_entities_to_context_to_be_deleted_non_generic()
        {
            TrackEntitiesTestNonGeneric((c, e) => c.Remove(e), (c, e) => c.Remove(e), EntityState.Deleted);
        }

        [Fact]
        public void Can_add_new_entities_to_context_non_generic_graph()
        {
            TrackEntitiesTestNonGeneric((c, e) => c.Add(e), (c, e) => c.Add(e), EntityState.Added);
        }

        [Fact]
        public void Can_add_existing_entities_to_context_to_be_attached_non_generic_graph()
        {
            TrackEntitiesTestNonGeneric((c, e) => c.Attach(e), (c, e) => c.Attach(e), EntityState.Unchanged);
        }

        [Fact]
        public void Can_add_existing_entities_to_context_to_be_updated_non_generic_graph()
        {
            TrackEntitiesTestNonGeneric((c, e) => c.Update(e), (c, e) => c.Update(e), EntityState.Modified);
        }

        private static void TrackEntitiesTestNonGeneric(
            Func<DbContext, object, EntityEntry> categoryAdder,
            Func<DbContext, object, EntityEntry> productAdder, EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category1 = new Category { Id = 1, Name = "Beverages" };
                var category2 = new Category { Id = 2, Name = "Foods" };
                var product1 = new Product { Id = 1, Name = "Marmite", Price = 7.99m };
                var product2 = new Product { Id = 2, Name = "Bovril", Price = 4.99m };

                var categoryEntry1 = categoryAdder(context, category1);
                var categoryEntry2 = categoryAdder(context, category2);
                var productEntry1 = productAdder(context, product1);
                var productEntry2 = productAdder(context, product2);

                Assert.Same(category1, categoryEntry1.Entity);
                Assert.Same(category2, categoryEntry2.Entity);
                Assert.Same(product1, productEntry1.Entity);
                Assert.Same(product2, productEntry2.Entity);

                Assert.Same(category1, categoryEntry1.Entity);
                Assert.Equal(expectedState, categoryEntry1.State);
                Assert.Same(category2, categoryEntry2.Entity);
                Assert.Equal(expectedState, categoryEntry2.State);

                Assert.Same(product1, productEntry1.Entity);
                Assert.Equal(expectedState, productEntry1.State);
                Assert.Same(product2, productEntry2.Entity);
                Assert.Equal(expectedState, productEntry2.State);

                Assert.Same(categoryEntry1.GetInfrastructure(), context.Entry(category1).GetInfrastructure());
                Assert.Same(categoryEntry2.GetInfrastructure(), context.Entry(category2).GetInfrastructure());
                Assert.Same(productEntry1.GetInfrastructure(), context.Entry(product1).GetInfrastructure());
                Assert.Same(productEntry2.GetInfrastructure(), context.Entry(product2).GetInfrastructure());
            }
        }

        [Fact]
        public void Can_add_multiple_existing_entities_to_context_to_be_deleted_Enumerable()
        {
            TrackMultipleEntitiesTestEnumerable((c, e) => c.RemoveRange(e), (c, e) => c.RemoveRange(e), EntityState.Deleted);
        }

        [Fact]
        public void Can_add_multiple_new_entities_to_context_Enumerable_graph()
        {
            TrackMultipleEntitiesTestEnumerable((c, e) => c.AddRange(e), (c, e) => c.AddRange(e), EntityState.Added);
        }

        [Fact]
        public void Can_add_multiple_existing_entities_to_context_to_be_attached_Enumerable_graph()
        {
            TrackMultipleEntitiesTestEnumerable((c, e) => c.AttachRange(e), (c, e) => c.AttachRange(e), EntityState.Unchanged);
        }

        [Fact]
        public void Can_add_multiple_existing_entities_to_context_to_be_updated_Enumerable_graph()
        {
            TrackMultipleEntitiesTestEnumerable((c, e) => c.UpdateRange(e), (c, e) => c.UpdateRange(e), EntityState.Modified);
        }

        private static void TrackMultipleEntitiesTestEnumerable(
            Action<DbContext, IEnumerable<object>> categoryAdder,
            Action<DbContext, IEnumerable<object>> productAdder, EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category1 = new Category { Id = 1, Name = "Beverages" };
                var category2 = new Category { Id = 2, Name = "Foods" };
                var product1 = new Product { Id = 1, Name = "Marmite", Price = 7.99m };
                var product2 = new Product { Id = 2, Name = "Bovril", Price = 4.99m };

                categoryAdder(context, new List<Category> { category1, category2 });
                productAdder(context, new List<Product> { product1, product2 });

                Assert.Same(category1, context.Entry(category1).Entity);
                Assert.Same(category2, context.Entry(category2).Entity);
                Assert.Same(product1, context.Entry(product1).Entity);
                Assert.Same(product2, context.Entry(product2).Entity);

                Assert.Same(category1, context.Entry(category1).Entity);
                Assert.Equal(expectedState, context.Entry(category1).State);
                Assert.Same(category2, context.Entry(category2).Entity);
                Assert.Equal(expectedState, context.Entry(category2).State);

                Assert.Same(product1, context.Entry(product1).Entity);
                Assert.Equal(expectedState, context.Entry(product1).State);
                Assert.Same(product2, context.Entry(product2).Entity);
                Assert.Equal(expectedState, context.Entry(product2).State);
            }
        }

        [Fact]
        public void Can_add_existing_entities_with_default_value_to_context_to_be_deleted_non_generic()
        {
            TrackEntitiesDefaultValuesTestNonGeneric((c, e) => c.Remove(e), (c, e) => c.Remove(e), EntityState.Deleted);
        }

        [Fact]
        public void Can_add_new_entities_with_default_value_to_context_non_generic_graph()
        {
            TrackEntitiesDefaultValuesTestNonGeneric((c, e) => c.Add(e), (c, e) => c.Add(e), EntityState.Added);
        }

        [Fact]
        public void Can_add_existing_entities_with_default_value_to_context_to_be_attached_non_generic_graph()
        {
            TrackEntitiesDefaultValuesTestNonGeneric((c, e) => c.Attach(e), (c, e) => c.Attach(e), EntityState.Unchanged);
        }

        [Fact]
        public void Can_add_existing_entities_with_default_value_to_context_to_be_updated_non_generic_graph()
        {
            TrackEntitiesDefaultValuesTestNonGeneric((c, e) => c.Update(e), (c, e) => c.Update(e), EntityState.Modified);
        }

        // Issue #3890
        private static void TrackEntitiesDefaultValuesTestNonGeneric(
            Func<DbContext, object, EntityEntry> categoryAdder,
            Func<DbContext, object, EntityEntry> productAdder, EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category1 = new Category { Id = 0, Name = "Beverages" };
                var product1 = new Product { Id = 0, Name = "Marmite", Price = 7.99m };

                var categoryEntry1 = categoryAdder(context, category1);
                var productEntry1 = productAdder(context, product1);

                Assert.Same(category1, categoryEntry1.Entity);
                Assert.Same(product1, productEntry1.Entity);

                Assert.Same(category1, categoryEntry1.Entity);
                Assert.Equal(expectedState, categoryEntry1.State);

                Assert.Same(product1, productEntry1.Entity);
                Assert.Equal(expectedState, productEntry1.State);

                Assert.Same(categoryEntry1.GetInfrastructure(), context.Entry(category1).GetInfrastructure());
                Assert.Same(productEntry1.GetInfrastructure(), context.Entry(product1).GetInfrastructure());
            }
        }

        [Fact]
        public void Can_add_multiple_existing_entities_with_default_values_to_context_to_be_deleted_Enumerable()
        {
            TrackMultipleEntitiesDefaultValueTestEnumerable((c, e) => c.RemoveRange(e), (c, e) => c.RemoveRange(e), EntityState.Deleted);
        }

        [Fact]
        public void Can_add_multiple_new_entities_with_default_values_to_context_Enumerable_graph()
        {
            TrackMultipleEntitiesDefaultValueTestEnumerable((c, e) => c.AddRange(e), (c, e) => c.AddRange(e), EntityState.Added);
        }

        [Fact]
        public void Can_add_multiple_existing_entities_with_default_values_to_context_to_be_attached_Enumerable_graph()
        {
            TrackMultipleEntitiesDefaultValueTestEnumerable((c, e) => c.AttachRange(e), (c, e) => c.AttachRange(e), EntityState.Unchanged);
        }

        [Fact]
        public void Can_add_multiple_existing_entities_with_default_values_to_context_to_be_updated_Enumerable_graph()
        {
            TrackMultipleEntitiesDefaultValueTestEnumerable((c, e) => c.UpdateRange(e), (c, e) => c.UpdateRange(e), EntityState.Modified);
        }

        // Issue #3890
        private static void TrackMultipleEntitiesDefaultValueTestEnumerable(
            Action<DbContext, IEnumerable<object>> categoryAdder,
            Action<DbContext, IEnumerable<object>> productAdder, EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category1 = new Category { Id = 0, Name = "Beverages" };
                var product1 = new Product { Id = 0, Name = "Marmite", Price = 7.99m };

                categoryAdder(context, new List<Category> { category1 });
                productAdder(context, new List<Product> { product1 });

                Assert.Same(category1, context.Entry(category1).Entity);
                Assert.Same(product1, context.Entry(product1).Entity);

                Assert.Same(category1, context.Entry(category1).Entity);
                Assert.Equal(expectedState, context.Entry(category1).State);

                Assert.Same(product1, context.Entry(product1).Entity);
                Assert.Equal(expectedState, context.Entry(product1).State);
            }
        }

        [Fact]
        public void Can_add_no_existing_entities_to_context_to_be_deleted_Enumerable()
        {
            TrackNoEntitiesTestEnumerable((c, e) => c.RemoveRange(e), (c, e) => c.RemoveRange(e));
        }

        [Fact]
        public void Can_add_no_new_entities_to_context_Enumerable_graph()
        {
            TrackNoEntitiesTestEnumerable((c, e) => c.AddRange(e), (c, e) => c.AddRange(e));
        }

        [Fact]
        public void Can_add_no_existing_entities_to_context_to_be_attached_Enumerable_graph()
        {
            TrackNoEntitiesTestEnumerable((c, e) => c.AttachRange(e), (c, e) => c.AttachRange(e));
        }

        [Fact]
        public void Can_add_no_existing_entities_to_context_to_be_updated_Enumerable_graph()
        {
            TrackNoEntitiesTestEnumerable((c, e) => c.UpdateRange(e), (c, e) => c.UpdateRange(e));
        }

        private static void TrackNoEntitiesTestEnumerable(
            Action<DbContext, IEnumerable<object>> categoryAdder,
            Action<DbContext, IEnumerable<object>> productAdder)
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                categoryAdder(context, new HashSet<Category>());
                productAdder(context, new HashSet<Product>());
                Assert.Empty(context.ChangeTracker.Entries());
            }
        }

        [Fact]
        public void Can_add_new_entities_to_context_with_key_generation_graph()
        {
            TrackEntitiesWithKeyGenerationTest((c, e) => c.Add(e).Entity);
        }

        private static void TrackEntitiesWithKeyGenerationTest(Func<DbContext, TheGu, TheGu> adder)
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var gu1 = new TheGu { ShirtColor = "Red" };
                var gu2 = new TheGu { ShirtColor = "Still Red" };

                Assert.Same(gu1, adder(context, gu1));
                Assert.Same(gu2, adder(context, gu2));
                Assert.NotEqual(default(Guid), gu1.Id);
                Assert.NotEqual(default(Guid), gu2.Id);
                Assert.NotEqual(gu1.Id, gu2.Id);

                var categoryEntry = context.Entry(gu1);
                Assert.Same(gu1, categoryEntry.Entity);
                Assert.Equal(EntityState.Added, categoryEntry.State);

                categoryEntry = context.Entry(gu2);
                Assert.Same(gu2, categoryEntry.Entity);
                Assert.Equal(EntityState.Added, categoryEntry.State);
            }
        }

        [Fact]
        public void Can_use_Remove_to_change_entity_state()
        {
            ChangeStateWithMethod((c, e) => c.Remove(e), EntityState.Detached, EntityState.Deleted);
            ChangeStateWithMethod((c, e) => c.Remove(e), EntityState.Unchanged, EntityState.Deleted);
            ChangeStateWithMethod((c, e) => c.Remove(e), EntityState.Deleted, EntityState.Deleted);
            ChangeStateWithMethod((c, e) => c.Remove(e), EntityState.Modified, EntityState.Deleted);
            ChangeStateWithMethod((c, e) => c.Remove(e), EntityState.Added, EntityState.Detached);
        }

        [Fact]
        public void Can_use_graph_Add_to_change_entity_state()
        {
            ChangeStateWithMethod((c, e) => c.Add(e), EntityState.Detached, EntityState.Added);
            ChangeStateWithMethod((c, e) => c.Add(e), EntityState.Unchanged, EntityState.Added);
            ChangeStateWithMethod((c, e) => c.Add(e), EntityState.Deleted, EntityState.Added);
            ChangeStateWithMethod((c, e) => c.Add(e), EntityState.Modified, EntityState.Added);
            ChangeStateWithMethod((c, e) => c.Add(e), EntityState.Added, EntityState.Added);
        }

        [Fact]
        public void Can_use_graph_Attach_to_change_entity_state()
        {
            ChangeStateWithMethod((c, e) => c.Attach(e), EntityState.Detached, EntityState.Unchanged);
            ChangeStateWithMethod((c, e) => c.Attach(e), EntityState.Unchanged, EntityState.Unchanged);
            ChangeStateWithMethod((c, e) => c.Attach(e), EntityState.Deleted, EntityState.Unchanged);
            ChangeStateWithMethod((c, e) => c.Attach(e), EntityState.Modified, EntityState.Unchanged);
            ChangeStateWithMethod((c, e) => c.Attach(e), EntityState.Added, EntityState.Unchanged);
        }

        [Fact]
        public void Can_use_graph_Update_to_change_entity_state()
        {
            ChangeStateWithMethod((c, e) => c.Update(e), EntityState.Detached, EntityState.Modified);
            ChangeStateWithMethod((c, e) => c.Update(e), EntityState.Unchanged, EntityState.Modified);
            ChangeStateWithMethod((c, e) => c.Update(e), EntityState.Deleted, EntityState.Modified);
            ChangeStateWithMethod((c, e) => c.Update(e), EntityState.Modified, EntityState.Modified);
            ChangeStateWithMethod((c, e) => c.Update(e), EntityState.Added, EntityState.Modified);
        }

        private void ChangeStateWithMethod(Action<DbContext, object> action, EntityState initialState, EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var entity = new Category { Id = 1, Name = "Beverages" };
                var entry = context.Entry(entity);

                entry.State = initialState;

                action(context, entity);

                Assert.Equal(expectedState, entry.State);
            }
        }

        [Fact] // Issue #1246
        public void Can_attach_with_inconsistent_FK_principal_first_fully_fixed_up()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product> { product };

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Detached, context.Entry(product).State);

                context.Attach(product);

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);

                // Dependent is Unchanged here because the FK change happened before it was attached
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_attach_with_inconsistent_FK_dependent_first_fully_fixed_up()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product> { product };

                context.Attach(product);

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);

                context.Attach(category);

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_attach_with_inconsistent_FK_principal_first_collection_not_fixed_up()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product>();

                context.Attach(category);

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Detached, context.Entry(product).State);

                context.Attach(product);

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_attach_with_inconsistent_FK_dependent_first_collection_not_fixed_up()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product>();

                context.Attach(product);

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);

                context.Attach(category);

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_attach_with_inconsistent_FK_principal_first_reference_not_fixed_up()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite" };
                category.Products = new List<Product> { product };

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Null(product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Detached, context.Entry(product).State);

                context.Attach(product);

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Null(product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_attach_with_inconsistent_FK_dependent_first_reference_not_fixed_up()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite" };
                category.Products = new List<Product> { product };

                context.Attach(product);

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Null(product.Category);
                Assert.Equal(EntityState.Detached, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);

                context.Attach(category);

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Null(product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_set_set_to_Unchanged_with_inconsistent_FK_principal_first_fully_fixed_up()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product> { product };

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Detached, context.Entry(product).State);

                context.Entry(product).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);

                // Dependent is Unchanged here because the FK change happened before it was attached
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_set_set_to_Unchanged_with_inconsistent_FK_dependent_first_fully_fixed_up()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product> { product };

                context.Entry(product).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Detached, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_set_set_to_Unchanged_with_inconsistent_FK_principal_first_collection_not_fixed_up()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product>();

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Detached, context.Entry(product).State);

                context.Entry(product).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_set_set_to_Unchanged_with_inconsistent_FK_dependent_first_collection_not_fixed_up()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product>();

                context.Entry(product).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Detached, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category, product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_set_set_to_Unchanged_with_inconsistent_FK_principal_first_reference_not_fixed_up()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite" };
                category.Products = new List<Product> { product };

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Null(product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Detached, context.Entry(product).State);

                context.Entry(product).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Null(product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_set_set_to_Unchanged_with_inconsistent_FK_dependent_first_reference_not_fixed_up()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite" };
                category.Products = new List<Product> { product };

                context.Entry(product).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Null(product.Category);
                Assert.Equal(EntityState.Detached, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Null(product.Category);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_attach_with_inconsistent_FK_principal_first_fully_fixed_up_with_tracked_FK_match()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category7 = context.Attach(new Category { Id = 7, Products = new List<Product>() }).Entity;

                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product> { product };

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category, product.Category);
                Assert.Empty(category7.Products);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Detached, context.Entry(product).State);

                context.Attach(product);

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);

                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_attach_with_inconsistent_FK_dependent_first_fully_fixed_up_with_tracked_FK_match()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category7 = context.Attach(new Category { Id = 7, Products = new List<Product>() }).Entity;

                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product> { product };

                context.Attach(product);

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Detached, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);

                context.Attach(category);

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_attach_with_inconsistent_FK_principal_first_collection_not_fixed_up_with_tracked_FK_match()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category7 = context.Attach(new Category { Id = 7, Products = new List<Product>() }).Entity;

                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product>();

                context.Attach(category);

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category, product.Category);
                Assert.Empty(category7.Products);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Detached, context.Entry(product).State);

                context.Attach(product);

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_attach_with_inconsistent_FK_dependent_first_collection_not_fixed_up_with_tracked_FK_match()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category7 = context.Attach(new Category { Id = 7, Products = new List<Product>() }).Entity;

                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product>();

                context.Attach(product);

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Detached, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);

                context.Attach(category);

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_attach_with_inconsistent_FK_principal_first_reference_not_fixed_up_with_tracked_FK_match()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category7 = context.Attach(new Category { Id = 7, Products = new List<Product>() }).Entity;

                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite" };
                category.Products = new List<Product> { product };

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Null(product.Category);
                Assert.Empty(category7.Products);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Detached, context.Entry(product).State);

                context.Attach(product);

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_attach_with_inconsistent_FK_dependent_first_reference_not_fixed_up_with_tracked_FK_match()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category7 = context.Attach(new Category { Id = 7, Products = new List<Product>() }).Entity;

                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite" };
                category.Products = new List<Product> { product };

                context.Attach(product);

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Detached, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);

                context.Attach(category);

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_set_set_to_Unchanged_with_inconsistent_FK_principal_first_fully_fixed_up_with_tracked_FK_match()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category7 = context.Attach(new Category { Id = 7, Products = new List<Product>() }).Entity;

                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product> { product };

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category, product.Category);
                Assert.Empty(category7.Products);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Detached, context.Entry(product).State);

                context.Entry(product).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_set_set_to_Unchanged_with_inconsistent_FK_dependent_first_fully_fixed_up_with_tracked_FK_match()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category7 = context.Attach(new Category { Id = 7, Products = new List<Product>() }).Entity;

                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product> { product };

                context.Entry(product).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Detached, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_set_set_to_Unchanged_with_inconsistent_FK_principal_first_collection_not_fixed_up_with_tracked_FK_match()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category7 = context.Attach(new Category { Id = 7, Products = new List<Product>() }).Entity;

                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product>();

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category, product.Category);
                Assert.Empty(category7.Products);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Detached, context.Entry(product).State);

                context.Entry(product).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_set_set_to_Unchanged_with_inconsistent_FK_dependent_first_collection_not_fixed_up_with_tracked_FK_match()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category7 = context.Attach(new Category { Id = 7, Products = new List<Product>() }).Entity;

                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite", Category = category };
                category.Products = new List<Product>();

                context.Entry(product).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Detached, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Empty(category.Products);
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_set_set_to_Unchanged_with_inconsistent_FK_principal_first_reference_not_fixed_up_with_tracked_FK_match()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category7 = context.Attach(new Category { Id = 7, Products = new List<Product>() }).Entity;

                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite" };
                category.Products = new List<Product> { product };

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Null(product.Category);
                Assert.Empty(category7.Products);
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Detached, context.Entry(product).State);

                context.Entry(product).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact] // Issue #1246
        public void Can_set_set_to_Unchanged_with_inconsistent_FK_dependent_first_reference_not_fixed_up_with_tracked_FK_match()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                var category7 = context.Attach(new Category { Id = 7, Products = new List<Product>() }).Entity;

                var category = new Category { Id = 1, Name = "Beverages" };
                var product = new Product { Id = 1, CategoryId = 7, Name = "Marmite" };
                category.Products = new List<Product> { product };

                context.Entry(product).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Detached, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);

                context.Entry(category).State = EntityState.Unchanged;

                Assert.Equal(7, product.CategoryId);
                Assert.Same(product, category.Products.Single());
                Assert.Same(category7, product.Category);
                Assert.Same(product, category7.Products.Single());
                Assert.Equal(EntityState.Unchanged, context.Entry(category).State);
                Assert.Equal(EntityState.Unchanged, context.Entry(product).State);
            }
        }

        [Fact]
        public void Context_can_build_model_using_DbSet_properties()
        {
            using (var context = new EarlyLearningCenter(TestHelpers.Instance.CreateServiceProvider()))
            {
                Assert.Equal(
                    new[] { typeof(Category).FullName, typeof(Product).FullName, typeof(TheGu).FullName },
                    context.Model.GetEntityTypes().Select(e => e.Name).ToArray());

                var categoryType = context.Model.FindEntityType(typeof(Category));
                Assert.Equal("Id", categoryType.FindPrimaryKey().Properties.Single().Name);
                Assert.Equal(
                    new[] { "Id", "Name" },
                    categoryType.GetProperties().Select(p => p.Name).ToArray());

                var productType = context.Model.FindEntityType(typeof(Product));
                Assert.Equal("Id", productType.FindPrimaryKey().Properties.Single().Name);
                Assert.Equal(
                    new[] { "Id", "CategoryId", "Name", "Price" },
                    productType.GetProperties().Select(p => p.Name).ToArray());

                var guType = context.Model.FindEntityType(typeof(TheGu));
                Assert.Equal("Id", guType.FindPrimaryKey().Properties.Single().Name);
                Assert.Equal(
                    new[] { "Id", "ShirtColor" },
                    guType.GetProperties().Select(p => p.Name).ToArray());
            }
        }

        [Fact]
        public void Context_will_use_explicit_model_if_set_in_config()
        {
            var model = new Model();
            model.AddEntityType(typeof(TheGu));

            using (var context = new EarlyLearningCenter(
                TestHelpers.Instance.CreateServiceProvider(),
                new DbContextOptionsBuilder().UseModel(model).Options))
            {
                Assert.Equal(
                    new[] { typeof(TheGu).FullName },
                    context.Model.GetEntityTypes().Select(e => e.Name).ToArray());
            }
        }

        [Fact]
        public void Context_initializes_all_DbSet_properties_with_setters()
        {
            using (var context = new ContextWithSets())
            {
                Assert.NotNull(context.Products);
                Assert.NotNull(context.Categories);
                Assert.NotNull(context.GetGus());
                Assert.Null(context.NoSetter);
            }
        }

        private class ContextWithSets : DbContext
        {
            public DbSet<Product> Products { get; set; }
            public DbSet<Category> Categories { get; private set; }
            private DbSet<TheGu> Gus { get; set; }

            public DbSet<Random> NoSetter { get; } = null;

            public DbSet<TheGu> GetGus()
            {
                return Gus;
            }
        }

        [Fact]
        public void SaveChanges_doesnt_call_Database_when_nothing_is_dirty()
        {
            var database = new Mock<IDatabase>();

            var servicesMock = new Mock<IDatabaseProviderServices>();
            servicesMock.Setup(m => m.Database).Returns(database.Object);
            servicesMock.Setup(m => m.ModelSource).Returns(new Mock<ModelSource>(new DbSetFinder(), new CoreConventionSetBuilder(), new ModelCustomizer(), new ModelCacheKeyFactory())
            { CallBase = true }.Object);
            servicesMock
                .Setup(m => m.ModelValidator)
                .Returns(new LoggingModelValidator(new Logger<LoggingModelValidator>(new LoggerFactory())));

            var sourceMock = new Mock<IDatabaseProvider>();
            sourceMock.Setup(m => m.IsConfigured(It.IsAny<IDbContextOptions>())).Returns(true);
            sourceMock.Setup(m => m.GetProviderServices(It.IsAny<IServiceProvider>())).Returns(servicesMock.Object);

            var services = new ServiceCollection();
            services.AddEntityFramework();
            services.AddSingleton(sourceMock.Object);
            var serviceProvider = services.BuildServiceProvider();

            using (var context = new EarlyLearningCenter(serviceProvider, new DbContextOptionsBuilder().Options))
            {
                context.Entry(new Category { Id = 1 }).State = EntityState.Unchanged;
                context.Entry(new Category { Id = 2 }).State = EntityState.Unchanged;
                Assert.Equal(2, context.ChangeTracker.Entries().Count());

                context.SaveChanges();
            }

            database.Verify(
                s => s.SaveChangesAsync(It.IsAny<IReadOnlyList<InternalEntityEntry>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public void SaveChanges_only_passes_dirty_entries_to_Database()
        {
            var passedEntries = new List<IUpdateEntry>();
            var database = new Mock<IDatabase>();
            database.Setup(s => s.SaveChanges(It.IsAny<IReadOnlyList<IUpdateEntry>>()))
                .Callback<IEnumerable<IUpdateEntry>>(passedEntries.AddRange)
                .Returns(3);

            var valueGenMock = new Mock<IValueGeneratorSelector>();
            valueGenMock.Setup(m => m.Select(It.IsAny<IProperty>(), It.IsAny<IEntityType>())).Returns(Mock.Of<ValueGenerator>());

            var servicesMock = new Mock<IDatabaseProviderServices>();
            servicesMock.Setup(m => m.Database).Returns(database.Object);
            servicesMock.Setup(m => m.ValueGeneratorSelector).Returns(valueGenMock.Object);
            servicesMock.Setup(m => m.ModelSource).Returns(new Mock<ModelSource>(new DbSetFinder(), new CoreConventionSetBuilder(), new ModelCustomizer(), new ModelCacheKeyFactory())
            { CallBase = true }.Object);
            servicesMock
                .Setup(m => m.ModelValidator)
                .Returns(new LoggingModelValidator(new Logger<LoggingModelValidator>(new LoggerFactory())));

            var sourceMock = new Mock<IDatabaseProvider>();
            sourceMock.Setup(m => m.IsConfigured(It.IsAny<IDbContextOptions>())).Returns(true);
            sourceMock.Setup(m => m.GetProviderServices(It.IsAny<IServiceProvider>())).Returns(servicesMock.Object);

            var services = new ServiceCollection();
            services.AddEntityFramework();
            services.AddSingleton(sourceMock.Object);
            var serviceProvider = services.BuildServiceProvider();

            using (var context = new EarlyLearningCenter(serviceProvider, new DbContextOptionsBuilder().Options))
            {
                context.Entry(new Category { Id = 1 }).State = EntityState.Unchanged;
                context.Entry(new Category { Id = 2 }).State = EntityState.Modified;
                context.Entry(new Category { Id = 3 }).State = EntityState.Added;
                context.Entry(new Category { Id = 4 }).State = EntityState.Deleted;
                Assert.Equal(4, context.ChangeTracker.Entries().Count());

                context.SaveChanges();
            }

            Assert.Equal(3, passedEntries.Count);

            database.Verify(
                s => s.SaveChanges(It.IsAny<IReadOnlyList<InternalEntityEntry>>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveChangesAsync_only_passes_dirty_entries_to_Database()
        {
            var passedEntries = new List<IUpdateEntry>();
            var database = new Mock<IDatabase>();
            database.Setup(s => s.SaveChangesAsync(It.IsAny<IReadOnlyList<IUpdateEntry>>(), It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<IUpdateEntry>, CancellationToken>((e, c) => passedEntries.AddRange(e))
                .Returns(Task.FromResult(3));

            var valueGenMock = new Mock<IValueGeneratorSelector>();
            valueGenMock.Setup(m => m.Select(It.IsAny<IProperty>(), It.IsAny<IEntityType>())).Returns(Mock.Of<ValueGenerator>());

            var servicesMock = new Mock<IDatabaseProviderServices>();
            servicesMock.Setup(m => m.Database).Returns(database.Object);
            servicesMock.Setup(m => m.ValueGeneratorSelector).Returns(valueGenMock.Object);
            servicesMock.Setup(m => m.ModelSource).Returns(new Mock<ModelSource>(new DbSetFinder(), new CoreConventionSetBuilder(), new ModelCustomizer(), new ModelCacheKeyFactory())
            { CallBase = true }.Object);
            servicesMock
                .Setup(m => m.ModelValidator)
                .Returns(new LoggingModelValidator(new Logger<LoggingModelValidator>(new LoggerFactory())));

            var sourceMock = new Mock<IDatabaseProvider>();
            sourceMock.Setup(m => m.IsConfigured(It.IsAny<IDbContextOptions>())).Returns(true);
            sourceMock.Setup(m => m.GetProviderServices(It.IsAny<IServiceProvider>())).Returns(servicesMock.Object);

            var services = new ServiceCollection();
            services.AddEntityFramework();
            services.AddSingleton(sourceMock.Object);
            var serviceProvider = services.BuildServiceProvider();

            using (var context = new EarlyLearningCenter(serviceProvider, new DbContextOptionsBuilder().Options))
            {
                context.Entry(new Category { Id = 1 }).State = EntityState.Unchanged;
                context.Entry(new Category { Id = 2 }).State = EntityState.Modified;
                context.Entry(new Category { Id = 3 }).State = EntityState.Added;
                context.Entry(new Category { Id = 4 }).State = EntityState.Deleted;
                Assert.Equal(4, context.ChangeTracker.Entries().Count());

                await context.SaveChangesAsync();
            }

            Assert.Equal(3, passedEntries.Count);

            database.Verify(
                s => s.SaveChangesAsync(It.IsAny<IReadOnlyList<InternalEntityEntry>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void Default_services_are_registered_when_parameterless_constructor_used()
        {
            using (var context = new EarlyLearningCenter())
            {
                Assert.IsType<DbSetFinder>(context.GetService<IDbSetFinder>());
            }
        }

        [Fact]
        public void Default_context_scoped_services_are_registered_when_parameterless_constructor_used()
        {
            using (var context = new EarlyLearningCenter())
            {
                Assert.IsType<InternalEntityEntryFactory>(context.GetService<IInternalEntityEntryFactory>());
            }
        }

        [Fact]
        public void Can_get_singleton_service_from_scoped_configuration()
        {
            using (var context = new EarlyLearningCenter())
            {
                Assert.IsType<StateManager>(context.GetService<IStateManager>());
            }
        }

        [Fact]
        public void Can_start_with_custom_services_by_passing_in_base_service_provider()
        {
            var factory = Mock.Of<INavigationFixer>();
            var serviceCollection = new ServiceCollection()
                .AddSingleton<IDbSetFinder, DbSetFinder>()
                .AddSingleton<IDbSetSource, DbSetSource>()
                .AddSingleton<IEntityMaterializerSource, EntityMaterializerSource>()
                .AddSingleton<IMemberMapper, MemberMapper>()
                .AddSingleton<IFieldMatcher, FieldMatcher>()
                .AddSingleton<DatabaseProviderSelector>()
                .AddScoped<IDbSetInitializer, DbSetInitializer>()
                .AddScoped<IDbContextServices, DbContextServices>()
                .AddLogging()
                .AddSingleton(factory);

            var provider = serviceCollection.BuildServiceProvider();

            using (var context = new EarlyLearningCenter(provider))
            {
                Assert.Same(factory, context.GetService<INavigationFixer>());
            }
        }

        [Fact]
        public void Required_low_level_services_are_added_if_needed()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddEntityFramework();

            var provider = serviceCollection.BuildServiceProvider();

            Assert.IsType<LoggerFactory>(provider.GetRequiredService<ILoggerFactory>());
        }

        [Fact]
        public void Required_low_level_services_are_not_added_if_already_present()
        {
            var serviceCollection = new ServiceCollection();

            var loggerFactory = new FakeLoggerFactory();

            serviceCollection
                .AddSingleton<ILoggerFactory>(loggerFactory)
                .AddEntityFramework();

            var provider = serviceCollection.BuildServiceProvider();

            Assert.Same(loggerFactory, provider.GetRequiredService<ILoggerFactory>());
        }

        [Fact]
        public void Low_level_services_can_be_replaced_after_being_added()
        {
            var serviceCollection = new ServiceCollection();

            var loggerFactory = new FakeLoggerFactory();

            serviceCollection
                .AddEntityFramework();

            serviceCollection
                .AddSingleton<ILoggerFactory>(loggerFactory);

            var provider = serviceCollection.BuildServiceProvider();

            Assert.Same(loggerFactory, provider.GetRequiredService<ILoggerFactory>());
        }

        [Fact]
        public void Can_replace_already_registered_service_with_new_service()
        {
            var factory = Mock.Of<INavigationFixer>();
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddEntityFramework();
            serviceCollection.AddSingleton(factory);

            var provider = serviceCollection.BuildServiceProvider();

            using (var context = new EarlyLearningCenter(provider))
            {
                Assert.Same(factory, context.GetService<INavigationFixer>());
            }
        }

        [Fact]
        public void Can_set_known_singleton_services_using_instance_sugar()
        {
            var modelSource = Mock.Of<IModelSource>();

            var services = new ServiceCollection()
                .AddSingleton(modelSource);

            var provider = TestHelpers.Instance.CreateServiceProvider(services);

            using (var context = new EarlyLearningCenter(provider))
            {
                Assert.Same(modelSource, context.GetService<IModelSource>());
            }
        }

        [Fact]
        public void Can_set_known_singleton_services_using_type_activation()
        {
            var services = new ServiceCollection()
                .AddSingleton<IModelSource, FakeModelSource>();

            var provider = TestHelpers.Instance.CreateServiceProvider(services);

            using (var context = new EarlyLearningCenter(provider))
            {
                Assert.IsType<FakeModelSource>(context.GetService<IModelSource>());
            }
        }

        [Fact]
        public void Can_set_known_context_scoped_services_using_type_activation()
        {
            var services = new ServiceCollection()
                .AddScoped<IStateManager, FakeStateManager>();

            var provider = TestHelpers.Instance.CreateServiceProvider(services);

            using (var context = new EarlyLearningCenter(provider))
            {
                Assert.IsType<FakeStateManager>(context.GetService<IStateManager>());
            }
        }

        [Fact]
        public void Replaced_services_are_scoped_appropriately()
        {
            var services = new ServiceCollection();
            services
                .AddEntityFramework()
                .GetInfrastructure()
                .AddSingleton<IModelSource, FakeModelSource>()
                .AddScoped<IStateManager, FakeStateManager>();

            var provider = services.BuildServiceProvider();

            var context = new EarlyLearningCenter(provider);

            var modelSource = context.GetService<IModelSource>();

            context.Dispose();

            context = new EarlyLearningCenter(provider);

            var stateManager = context.GetService<IStateManager>();

            Assert.Same(stateManager, context.GetService<IStateManager>());

            Assert.Same(modelSource, context.GetService<IModelSource>());

            context.Dispose();

            context = new EarlyLearningCenter(provider);

            Assert.NotSame(stateManager, context.GetService<IStateManager>());

            Assert.Same(modelSource, context.GetService<IModelSource>());

            context.Dispose();
        }

        [Fact]
        public void Can_get_replaced_singleton_service_from_scoped_configuration()
        {
            var provider = new ServiceCollection()
                .AddEntityFramework()
                .GetInfrastructure()
                .AddSingleton<IEntityMaterializerSource, FakeEntityMaterializerSource>()
                .BuildServiceProvider();

            using (var context = new EarlyLearningCenter(provider))
            {
                Assert.IsType<FakeEntityMaterializerSource>(context.GetService<IEntityMaterializerSource>());
            }
        }

        private class Category
        {
            public int Id { get; set; }
            public string Name { get; set; }

            public List<Product> Products { get; set; }
        }

        private class Product
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }

            public int CategoryId { get; set; }
            public Category Category { get; set; }
        }

        private class TheGu
        {
            public Guid Id { get; set; }
            public string ShirtColor { get; set; }
        }

        private class EarlyLearningCenter : DbContext
        {
            public EarlyLearningCenter()
            {
            }

            public EarlyLearningCenter(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
            }

            public EarlyLearningCenter(IServiceProvider serviceProvider, DbContextOptions options)
                : base(serviceProvider, options)
            {
            }

            public DbSet<Product> Products { get; set; }
            public DbSet<Category> Categories { get; set; }
            public DbSet<TheGu> Gus { get; set; }

            protected internal override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseInMemoryDatabase();
            }

            protected internal override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder
                    .Entity<Category>().HasMany(e => e.Products).WithOne(e => e.Category);
            }
        }

        private class FakeEntityMaterializerSource : EntityMaterializerSource
        {
            public FakeEntityMaterializerSource(IMemberMapper memberMapper)
                : base(memberMapper)
            {
            }
        }

        private class FakeLoggerFactory : ILoggerFactory
        {
            public ILogger CreateLogger(string name)
            {
                return null;
            }

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public void Dispose()
            {
            }
        }

        private class FakeModelSource : IModelSource
        {
            public virtual IModel GetModel(DbContext context, IConventionSetBuilder conventionSetBuilder, IModelValidator validator = null)
            {
                return null;
            }
        }

        [Fact]
        public void Context_with_defaults_can_be_used_as_service()
        {
            var services = new ServiceCollection();

            services
                .AddSingleton<FakeService>()
                .AddEntityFramework()
                .AddDbContext<ContextWithDefaults>();

            var serviceProvider = services.BuildServiceProvider();

            using (var context = serviceProvider.GetRequiredService<ContextWithDefaults>())
            {
                var contextServices = ((IInfrastructure<IServiceProvider>)context).Instance;

                Assert.NotNull(serviceProvider.GetRequiredService<FakeService>());
                Assert.NotSame(serviceProvider, contextServices);
                Assert.Equal(0, context.GetService<IDbContextOptions>().Extensions.Count());
            }
        }

        [Fact]
        public void Context_with_defaults_and_options_action_can_be_used_as_service()
        {
            var services = new ServiceCollection();
            var contextOptionsExtension = new FakeDbContextOptionsExtension();

            services
                .AddSingleton<FakeService>()
                .AddEntityFramework()
                .AddDbContext<ContextWithDefaults>(optionsBuilder
                    => ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(contextOptionsExtension));

            var serviceProvider = services.BuildServiceProvider();

            using (var context = serviceProvider.GetRequiredService<ContextWithDefaults>())
            {
                var options = context.GetService<IDbContextOptions>();

                Assert.NotNull(context.GetService<FakeService>());
                Assert.Equal(1, options.Extensions.Count());
                Assert.Same(contextOptionsExtension, options.Extensions.Single());
            }
        }

        [Fact]
        public void Context_with_service_provider_and_options_action_can_be_used_as_service()
        {
            var services = new ServiceCollection();
            var contextOptionsExtension = new FakeDbContextOptionsExtension();

            services
                .AddSingleton<FakeService>()
                .AddEntityFramework()
                .AddDbContext<ContextWithServiceProvider>(optionsBuilder
                    => ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(contextOptionsExtension));

            var serviceProvider = services.BuildServiceProvider();

            using (var context = serviceProvider.GetRequiredService<ContextWithServiceProvider>())
            {
                var options = context.GetService<IDbContextOptions>();

                Assert.NotNull(context.GetService<FakeService>());
                Assert.Equal(1, options.Extensions.Count());
                Assert.Same(contextOptionsExtension, options.Extensions.Single());
            }
        }

        [Fact]
        public void Context_with_options_and_options_action_can_be_used_as_service()
        {
            var services = new ServiceCollection();
            var contextOptionsExtension = new FakeDbContextOptionsExtension();

            services
                .AddSingleton<FakeService>()
                .AddEntityFramework()
                .AddDbContext<ContextWithOptions>(optionsBuilder
                    => ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(contextOptionsExtension));

            var serviceProvider = services.BuildServiceProvider();

            using (var context = serviceProvider.GetRequiredService<ContextWithOptions>())
            {
                var options = context.GetService<IDbContextOptions>();

                Assert.NotNull(context.GetService<FakeService>());
                Assert.Equal(1, options.Extensions.Count());
                Assert.Same(contextOptionsExtension, options.Extensions.Single());
            }
        }

        [Fact]
        public void Context_with_parameters_can_be_created_By_dbcontextactivator_createinstance()
        {
            var services = new ServiceCollection();
            var contextOptionsExtension = new FakeDbContextOptionsExtension();

            services
                .AddSingleton<FakeService>()
                .AddEntityFramework();

            var serviceProvider = services.BuildServiceProvider();
            var valueOfParamInt = 100;
            var valueOfParamStr = "Hello DbContext";
            using (var context = DbContextActivator.CreateInstance<ContextWithParameters>(serviceProvider, valueOfParamInt, valueOfParamStr))
            {
                Assert.NotNull(context.GetService<FakeService>());
                Assert.Equal(valueOfParamInt, context.ParamInt);
                Assert.Equal(valueOfParamStr, context.ParamStr);
            }
        }

        private class FakeService
        {
        }

        private class FakeDbContextOptionsExtension : IDbContextOptionsExtension
        {
            public virtual void ApplyServices(EntityFrameworkServicesBuilder builder)
            {
            }
        }

        private class ContextWithDefaults : DbContext
        {
            public DbSet<Product> Products { get; set; }
        }

        private class ContextWithServiceProvider : DbContext
        {
            public ContextWithServiceProvider(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
            }

            public DbSet<Product> Products { get; set; }
        }

        private class ContextWithOptions : DbContext
        {
            public ContextWithOptions(DbContextOptions<ContextWithOptions> contextOptions)
                : base(contextOptions)
            {
            }

            public DbSet<Product> Products { get; set; }
        }

        private class ContextWithParameters : DbContext
        {
            public int ParamInt { get; }
            public string ParamStr { get; }

            public ContextWithParameters(int paramInt, string paramStr)
            {
                ParamInt = paramInt;
                ParamStr = paramStr;
            }

            public DbSet<Product> Products { get; set; }
        }

        [Fact]
        public void Model_cannot_be_used_in_OnModelCreating()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddInMemoryDatabase()
                .AddDbContext<UseModelInOnModelCreatingContext>()
                .GetInfrastructure()
                .BuildServiceProvider();

            using (var context = serviceProvider.GetRequiredService<UseModelInOnModelCreatingContext>())
            {
                Assert.Equal(
                    CoreStrings.RecursiveOnModelCreating,
                    Assert.Throws<InvalidOperationException>(() => context.Model).Message);
            }
        }

        private class UseModelInOnModelCreatingContext : DbContext
        {
            public UseModelInOnModelCreatingContext(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
            }

            public DbSet<Product> Products { get; set; }

            protected internal override void OnModelCreating(ModelBuilder modelBuilder)
            {
                var _ = Model;
            }

            protected internal override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseInMemoryDatabase();
        }

        [Fact]
        public void Context_cannot_be_used_in_OnModelCreating()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddInMemoryDatabase()
                .AddDbContext<UseInOnModelCreatingContext>()
                .GetInfrastructure()
                .BuildServiceProvider();

            using (var context = serviceProvider.GetRequiredService<UseInOnModelCreatingContext>())
            {
                Assert.Equal(
                    CoreStrings.RecursiveOnModelCreating,
                    Assert.Throws<InvalidOperationException>(() => context.Products.ToList()).Message);
            }
        }

        private class UseInOnModelCreatingContext : DbContext
        {
            public UseInOnModelCreatingContext(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
            }

            public DbSet<Product> Products { get; set; }

            protected internal override void OnModelCreating(ModelBuilder modelBuilder)
                => Products.ToList();

            protected internal override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseInMemoryDatabase();
        }

        [Fact]
        public void Context_cannot_be_used_in_OnConfiguring()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddInMemoryDatabase()
                .AddDbContext<UseInOnConfiguringContext>()
                .GetInfrastructure()
                .BuildServiceProvider();

            using (var context = serviceProvider.GetRequiredService<UseInOnConfiguringContext>())
            {
                Assert.Equal(
                    CoreStrings.RecursiveOnConfiguring,
                    Assert.Throws<InvalidOperationException>(() => context.Products.ToList()).Message);
            }
        }

        private class UseInOnConfiguringContext : DbContext
        {
            public UseInOnConfiguringContext(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
            }

            public DbSet<Product> Products { get; set; }

            protected internal override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                Products.ToList();

                base.OnConfiguring(optionsBuilder);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SaveChanges_calls_DetectChanges_by_default(bool async)
        {
            var provider = TestHelpers.Instance.CreateServiceProvider();

            using (var context = new ButTheHedgehogContext(provider))
            {
                Assert.True(context.ChangeTracker.AutoDetectChangesEnabled);

                var product = context.Attach(new Product { Id = 1, Name = "Little Hedgehogs" }).Entity;

                product.Name = "Cracked Cookies";

                if (async)
                {
                    await context.SaveChangesAsync();
                }
                else
                {
                    context.SaveChanges();
                }
            }

            using (var context = new ButTheHedgehogContext(provider))
            {
                Assert.Equal("Cracked Cookies", context.Products.Single().Name);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Auto_DetectChanges_for_SaveChanges_can_be_switched_off(bool async)
        {
            var provider = TestHelpers.Instance.CreateServiceProvider();

            using (var context = new ButTheHedgehogContext(provider))
            {
                context.ChangeTracker.AutoDetectChangesEnabled = false;
                Assert.False(context.ChangeTracker.AutoDetectChangesEnabled);

                var product = context.Attach(new Product { Id = 1, Name = "Little Hedgehogs" }).Entity;

                product.Name = "Cracked Cookies";

                if (async)
                {
                    await context.SaveChangesAsync();
                }
                else
                {
                    context.SaveChanges();
                }
            }

            using (var context = new ButTheHedgehogContext(provider))
            {
                Assert.Empty(context.Products);
            }
        }

        private class ButTheHedgehogContext : DbContext
        {
            public ButTheHedgehogContext(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
            }

            public DbSet<Product> Products { get; set; }

            protected internal override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseInMemoryDatabase();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Entry_calls_DetectChanges_by_default(bool useGenericOverload)
        {
            using (var context = new ButTheHedgehogContext(TestHelpers.Instance.CreateServiceProvider()))
            {
                var entry = context.Attach(new Product { Id = 1, Name = "Little Hedgehogs" });

                entry.Entity.Name = "Cracked Cookies";

                Assert.Equal(EntityState.Unchanged, entry.State);

                if (useGenericOverload)
                {
                    context.Entry(entry.Entity);
                }
                else
                {
                    context.Entry((object)entry.Entity);
                }

                Assert.Equal(EntityState.Modified, entry.State);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Auto_DetectChanges_for_Entry_can_be_switched_off(bool useGenericOverload)
        {
            using (var context = new ButTheHedgehogContext(TestHelpers.Instance.CreateServiceProvider()))
            {
                context.ChangeTracker.AutoDetectChangesEnabled = false;

                var entry = context.Attach(new Product { Id = 1, Name = "Little Hedgehogs" });

                entry.Entity.Name = "Cracked Cookies";

                Assert.Equal(EntityState.Unchanged, entry.State);

                if (useGenericOverload)
                {
                    context.Entry(entry.Entity);
                }
                else
                {
                    context.Entry((object)entry.Entity);
                }

                Assert.Equal(EntityState.Unchanged, entry.State);
            }
        }

        [Fact]
        public void Add_Attach_Remove_Update_do_not_call_DetectChanges()
        {
            var provider = TestHelpers.Instance.CreateServiceProvider(new ServiceCollection().AddScoped<IChangeDetector, ChangeDetectorProxy>());
            using (var context = new ButTheHedgehogContext(provider))
            {
                var changeDetector = (ChangeDetectorProxy)context.GetService<IChangeDetector>();

                var id = 1;

                changeDetector.DetectChangesCalled = false;

                context.Add(new Product { Id = id++, Name = "Little Hedgehogs" });
                context.Add((object)new Product { Id = id++, Name = "Little Hedgehogs" });
                context.AddRange(new Product { Id = id++, Name = "Little Hedgehogs" });
                context.AddRange(new Product { Id = id++, Name = "Little Hedgehogs" });
                context.AddRange(new List<Product> { new Product { Id = id++, Name = "Little Hedgehogs" } });
                context.AddRange(new List<object> { new Product { Id = id++, Name = "Little Hedgehogs" } });
                context.Attach(new Product { Id = id++, Name = "Little Hedgehogs" });
                context.Attach((object)new Product { Id = id++, Name = "Little Hedgehogs" });
                context.AttachRange(new Product { Id = id++, Name = "Little Hedgehogs" });
                context.AttachRange(new Product { Id = id++, Name = "Little Hedgehogs" });
                context.AttachRange(new List<Product> { new Product { Id = id++, Name = "Little Hedgehogs" } });
                context.AttachRange(new List<object> { new Product { Id = id++, Name = "Little Hedgehogs" } });
                context.Update(new Product { Id = id++, Name = "Little Hedgehogs" });
                context.Update((object)new Product { Id = id++, Name = "Little Hedgehogs" });
                context.UpdateRange(new Product { Id = id++, Name = "Little Hedgehogs" });
                context.UpdateRange(new Product { Id = id++, Name = "Little Hedgehogs" });
                context.UpdateRange(new List<Product> { new Product { Id = id++, Name = "Little Hedgehogs" } });
                context.UpdateRange(new List<object> { new Product { Id = id++, Name = "Little Hedgehogs" } });
                context.Remove(new Product { Id = id++, Name = "Little Hedgehogs" });
                context.Remove((object)new Product { Id = id++, Name = "Little Hedgehogs" });
                context.RemoveRange(new Product { Id = id++, Name = "Little Hedgehogs" });
                context.RemoveRange(new Product { Id = id++, Name = "Little Hedgehogs" });
                context.RemoveRange(new List<Product> { new Product { Id = id++, Name = "Little Hedgehogs" } });
                context.RemoveRange(new List<object> { new Product { Id = id++, Name = "Little Hedgehogs" } });

                Assert.False(changeDetector.DetectChangesCalled);

                context.ChangeTracker.DetectChanges();

                Assert.True(changeDetector.DetectChangesCalled);
            }
        }

        private class ChangeDetectorProxy : ChangeDetector
        {
            public ChangeDetectorProxy(IEntityGraphAttacher attacher)
                : base(attacher)
            {
            }

            public bool DetectChangesCalled { get; set; }

            public override void DetectChanges(InternalEntityEntry entry)
            {
                DetectChangesCalled = true;

                base.DetectChanges(entry);
            }

            public override void DetectChanges(IStateManager stateManager)
            {
                DetectChangesCalled = true;

                base.DetectChanges(stateManager);
            }
        }

        private static Mock<InternalEntityEntry> CreateInternalEntryMock()
        {
            var entityTypeMock = new Mock<IEntityType>();
            entityTypeMock.Setup(e => e.GetProperties()).Returns(new IProperty[0]);

            entityTypeMock.As<IPropertyCountsAccessor>().Setup(e => e.Counts).Returns(new PropertyCounts(0, 0, 0, 0, 0, 0));

            var internalEntryMock = new Mock<InternalEntityEntry>(
                Mock.Of<IStateManager>(), entityTypeMock.Object);
            return internalEntryMock;
        }

        [Fact]
        public async void It_throws_object_disposed_exception()
        {
            var context = new DbContext(new DbContextOptions<DbContext>());
            context.Dispose();

            // methods (tests all paths)
            Assert.Throws<ObjectDisposedException>(() => context.Add(new object()));
            Assert.Throws<ObjectDisposedException>(() => context.Attach(new object()));
            Assert.Throws<ObjectDisposedException>(() => context.Update(new object()));
            Assert.Throws<ObjectDisposedException>(() => context.Remove(new object()));
            Assert.Throws<ObjectDisposedException>(() => context.SaveChanges());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => context.SaveChangesAsync());

            var methodCount = typeof(DbContext).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Count();
            var expectedMethodCount = 27;
            Assert.True(
                methodCount == expectedMethodCount,
                userMessage: $"Expected {expectedMethodCount} methods on DbContext but found {methodCount}. " +
                             "Update test to ensure all methods throw ObjectDisposedException after dispose.");

            // getters
            Assert.Throws<ObjectDisposedException>(() => context.ChangeTracker);
            Assert.Throws<ObjectDisposedException>(() => context.Model);

            var expectedProperties = new List<string> { "ChangeTracker", "Database", "Model" };

            Assert.True(expectedProperties.SequenceEqual(
                typeof(DbContext)
                    .GetProperties()
                    .Select(p => p.Name)
                    .OrderBy(s => s)
                    .ToList()),
                userMessage: "Unexpected properties on DbContext. " +
                             "Update test to ensure all getters throw ObjectDisposedException after dispose.");

            Assert.Throws<ObjectDisposedException>(() => ((IInfrastructure<IServiceProvider>)context).Instance);
        }

        [Fact]
        public void It_throws_with_derived_name()
        {
            var context = new EarlyLearningCenter();

            context.Dispose();

            var ex = Assert.Throws<ObjectDisposedException>(() => context.Model);
            if (!TestPlatformHelper.IsMono) 
            {
                Assert.Contains(nameof(EarlyLearningCenter), ex.Message);
            }
        }

        [Fact]
        public void It_disposes_scope()
        {
            var fakeServiceProvider = new FakeServiceProvider();
            var context = new DbContext(fakeServiceProvider, new DbContextOptions<DbContext>());

            var scopeService = Assert.IsType<FakeServiceProvider.FakeServiceScope>(context.GetService<IServiceScopeFactory>().CreateScope());

            Assert.False(scopeService.Disposed);

            context.Dispose();

            Assert.True(scopeService.Disposed);

            Assert.Throws<ObjectDisposedException>(() => ((IInfrastructure<IServiceProvider>)context).Instance);
        }

        public class FakeServiceProvider : IServiceProvider, IDisposable
        {
            private readonly IServiceProvider _realProvider;

            public FakeServiceProvider()
            {
                _realProvider = ((IInfrastructure<IServiceCollection>)new ServiceCollection().AddEntityFramework())
                    .Instance.BuildServiceProvider();
            }

            public bool Disposed { get; set; }

            public void Dispose()
            {
                Disposed = true;
            }

            public object GetService(Type serviceType)
            {
                if (serviceType == typeof(IServiceProvider))
                {
                    return this;
                }
                if (serviceType == typeof(IServiceScopeFactory))
                {
                    return new FakeServiceScopeFactory();
                }
                return _realProvider.GetService(serviceType);
            }

            public class FakeServiceScopeFactory : IServiceScopeFactory
            {
                public static FakeServiceScope Scope { get; } = new FakeServiceScope();
                public IServiceScope CreateScope() => Scope;
            }

            public class FakeServiceScope : IServiceScope
            {
                public bool Disposed { get; set; }
                public IServiceProvider ServiceProvider { get; set; } = new FakeServiceProvider();

                public void Dispose()
                {
                    Disposed = true;
                }
            }
        }
    }
}
