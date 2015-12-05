// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.Entity.Metadata.Conventions.Internal;
using Microsoft.Data.Entity.Metadata.Internal;
using Microsoft.Data.Entity.Tests;
using Microsoft.Data.Entity.Tests.Infrastructure;
using Microsoft.Data.Entity.Tests.TestUtilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Data.Entity.Internal.Tests
{
    public class RelationalModelValidatorTest : LoggingModelValidatorTest
    {
        [Fact]
        public virtual void Detects_duplicate_table_names()
        {
            var model = new Model();
            var entityA = model.AddEntityType(typeof(A));
            SetPrimaryKey(entityA);
            var entityB = model.AddEntityType(typeof(B));
            SetPrimaryKey(entityB);
            entityA.Relational().TableName = "Table";
            entityB.Relational().TableName = "Table";

            VerifyError(RelationalStrings.DuplicateTableName("Table", null, entityB.DisplayName()), model);
        }

        [Fact]
        public virtual void Detects_duplicate_table_names_with_schema()
        {
            var model = new Model();
            var entityA = model.AddEntityType(typeof(A));
            SetPrimaryKey(entityA);
            var entityB = model.AddEntityType(typeof(B));
            SetPrimaryKey(entityB);
            entityA.Relational().TableName = "Table";
            entityA.Relational().Schema = "Schema";
            entityB.Relational().TableName = "Table";
            entityB.Relational().Schema = "Schema";

            VerifyError(RelationalStrings.DuplicateTableName("Table", "Schema", entityB.DisplayName()), model);
        }

        [Fact]
        public virtual void Does_not_detect_duplicate_table_names_in_different_schema()
        {
            var model = new Model();
            var entityA = model.AddEntityType(typeof(A));
            SetPrimaryKey(entityA);
            var entityB = model.AddEntityType(typeof(B));
            SetPrimaryKey(entityB);
            entityA.Relational().TableName = "Table";
            entityA.Relational().Schema = "SchemaA";
            entityB.Relational().TableName = "Table";
            entityB.Relational().Schema = "SchemaB";

            Validate(model);
        }

        [Fact]
        public virtual void Does_not_detect_duplicate_table_names_for_inherited_entities()
        {
            var model = new Model();
            var entityA = model.AddEntityType(typeof(A));
            SetPrimaryKey(entityA);
            var entityC = model.AddEntityType(typeof(C));
            SetBaseType(entityC, entityA);

            Validate(model);
        }

        [Fact]
        public virtual void Detects_duplicate_column_names()
        {
            var modelBuilder = new ModelBuilder(new CoreConventionSetBuilder().CreateConventionSet());
            modelBuilder.Entity<Product>();
            modelBuilder.Entity<Product>().Property(b => b.Name).HasColumnName("Id");

            VerifyError(RelationalStrings.DuplicateColumnName("Id", typeof(Product).FullName, "Name"), modelBuilder.Model);
        }

        [Fact]
        public virtual void Passes_for_non_hierarchical_model()
        {
            var model = new Model();
            var entityA = model.AddEntityType(typeof(A));
            SetPrimaryKey(entityA);

            Validate(model);
        }

        [Fact]
        public virtual void Does_not_detect_non_instantiable_types()
        {
            var model = new Model();
            var entityAbstract = model.AddEntityType(typeof(Abstract));
            SetPrimaryKey(entityAbstract);
            var entityGeneric = model.AddEntityType(typeof(Generic<>));
            entityGeneric.HasBaseType(entityAbstract);

            Validate(model);
        }

        [Fact]
        public virtual void Detects_missing_discriminator_property()
        {
            var model = new Model();
            var entityA = model.AddEntityType(typeof(A));
            SetPrimaryKey(entityA);
            var entityC = model.AddEntityType(typeof(C));
            entityC.HasBaseType(entityA);

            VerifyError(RelationalStrings.NoDiscriminatorProperty(entityC.DisplayName()), model);
        }

        [Fact]
        public virtual void Detects_missing_discriminator_value_on_base()
        {
            var model = new Model();
            var entityA = model.AddEntityType(typeof(A));
            SetPrimaryKey(entityA);
            var entityAbstract = model.AddEntityType(typeof(Abstract));
            entityAbstract.HasBaseType(entityA);

            var discriminatorProperty = entityA.AddProperty("D", typeof(int));
            entityA.Relational().DiscriminatorProperty = discriminatorProperty;
            entityAbstract.Relational().DiscriminatorValue = 1;

            VerifyError(RelationalStrings.NoDiscriminatorValue(entityA.DisplayName()), model);
        }

        [Fact]
        public virtual void Detects_missing_discriminator_value_on_leaf()
        {
            var model = new Model();
            var entityAbstract = model.AddEntityType(typeof(Abstract));
            SetPrimaryKey(entityAbstract);
            var entityGeneric = model.AddEntityType(typeof(Generic<string>));
            entityGeneric.HasBaseType(entityAbstract);

            var discriminatorProperty = entityAbstract.AddProperty("D", typeof(int));
            entityAbstract.Relational().DiscriminatorProperty = discriminatorProperty;
            entityAbstract.Relational().DiscriminatorValue = 0;

            VerifyError(RelationalStrings.NoDiscriminatorValue(entityGeneric.DisplayName()), model);
        }

        protected override void SetBaseType(EntityType entityType, EntityType baseEntityType)
        {
            base.SetBaseType(entityType, baseEntityType);

            var discriminatorProperty = baseEntityType.GetOrAddProperty("Discriminator", typeof(string));
            baseEntityType.Relational().DiscriminatorProperty = discriminatorProperty;
            baseEntityType.Relational().DiscriminatorValue = baseEntityType.Name;
            entityType.Relational().DiscriminatorValue = entityType.Name;
        }

        protected class C : A
        {
        }

        protected abstract class Abstract : A
        {
        }

        protected class Generic<T> : Abstract
        {
        }

        private class Product
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        protected override ModelValidator CreateModelValidator()
            => new RelationalModelValidator(
                new Logger<RelationalModelValidator>(
                    new ListLoggerFactory(Log, l => l == typeof(RelationalModelValidator).FullName)),
                new TestAnnotationProvider());
    }
}
