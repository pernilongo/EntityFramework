// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Metadata.Internal
{
    public class InternalEntityTypeBuilder : InternalMetadataItemBuilder<EntityType>
    {
        public InternalEntityTypeBuilder([NotNull] EntityType metadata, [NotNull] InternalModelBuilder modelBuilder)
            : base(metadata, modelBuilder)
        {
        }

        public virtual InternalKeyBuilder PrimaryKey([NotNull] IReadOnlyList<string> propertyNames, ConfigurationSource configurationSource)
            => PrimaryKey(GetOrCreateProperties(propertyNames, configurationSource), configurationSource);

        public virtual InternalKeyBuilder PrimaryKey([NotNull] IReadOnlyList<PropertyInfo> clrProperties, ConfigurationSource configurationSource)
            => PrimaryKey(GetOrCreateProperties(clrProperties, configurationSource), configurationSource);

        private InternalKeyBuilder PrimaryKey(IReadOnlyList<Property> properties, ConfigurationSource configurationSource)
        {
            if (properties == null)
            {
                return null;
            }

            if (Metadata.FindPrimaryKey(properties) != null)
            {
                Metadata.SetPrimaryKey(properties, configurationSource);
                return HasKey(properties, configurationSource);
            }

            var primaryKeyConfigurationSource = Metadata.GetPrimaryKeyConfigurationSource();
            if (primaryKeyConfigurationSource.HasValue
                && !configurationSource.Overrides(primaryKeyConfigurationSource.Value))
            {
                return null;
            }

            var keyBuilder = HasKey(properties, configurationSource);
            if (keyBuilder == null)
            {
                return null;
            }

            var previousPrimaryKey = Metadata.FindPrimaryKey();
            Metadata.SetPrimaryKey(keyBuilder.Metadata.Properties, configurationSource, runConventions: false);
            UpdateReferencingForeignKeys(keyBuilder.Metadata);

            keyBuilder = ModelBuilder.Metadata.ConventionDispatcher.OnPrimaryKeySet(keyBuilder, previousPrimaryKey);

            if (previousPrimaryKey != null)
            {
                RemoveKeyIfUnused(previousPrimaryKey);
            }

            return keyBuilder;
        }

        private void UpdateReferencingForeignKeys(Key newKey)
        {
            foreach (var key in Metadata.GetDeclaredKeys().ToList())
            {
                if (key == newKey)
                {
                    continue;
                }

                var detachedRelationships = key.FindReferencingForeignKeys().ToList()
                    .Select(DetachRelationship).ToList();
                RemoveKey(key, ConfigurationSource.DataAnnotation);
                foreach (var relationshipSnapshot in detachedRelationships)
                {
                    relationshipSnapshot.Attach();
                }
            }
        }

        public virtual InternalKeyBuilder HasKey([NotNull] IReadOnlyList<string> propertyNames, ConfigurationSource configurationSource)
            => HasKey(GetOrCreateProperties(propertyNames, configurationSource), configurationSource);

        public virtual InternalKeyBuilder HasKey([NotNull] IReadOnlyList<PropertyInfo> clrProperties, ConfigurationSource configurationSource)
            => HasKey(GetOrCreateProperties(clrProperties, configurationSource), configurationSource);

        private InternalKeyBuilder HasKey(IReadOnlyList<Property> properties, ConfigurationSource configurationSource)
        {
            if (properties == null)
            {
                return null;
            }

            var actualProperties = GetActualProperties(properties, configurationSource);
            var key = Metadata.FindDeclaredKey(actualProperties);
            if (key == null)
            {
                if ((configurationSource != ConfigurationSource.Explicit) // let it throw for explicit
                    && (actualProperties.Any(p => p.FindContainingForeignKeys().Any(k => k.DeclaringEntityType != Metadata))
                    || actualProperties.Any(p => !p.Builder.CanSetRequired(true, configurationSource))))
                {
                    return null;
                }

                foreach (var actualProperty in actualProperties)
                {
                    actualProperty.Builder.IsRequired(true, configurationSource);
                }

                key = Metadata.AddKey(actualProperties, configurationSource);
            }
            else
            {
                key.UpdateConfigurationSource(configurationSource);
            }

            return key?.Builder;
        }

        public virtual ConfigurationSource? RemoveKey(
            [NotNull] Key key, ConfigurationSource configurationSource, bool runConventions = true)
        {
            var currentConfigurationSource = key.GetConfigurationSource();
            if (!configurationSource.Overrides(currentConfigurationSource))
            {
                return null;
            }

            foreach (var foreignKey in key.FindReferencingForeignKeys().ToList())
            {
                var removed = foreignKey.DeclaringEntityType.Builder.RemoveForeignKey(foreignKey, configurationSource, runConventions);
                Debug.Assert(removed.HasValue);
            }

            var removedKey = Metadata.RemoveKey(key.Properties, runConventions);
            if (removedKey == null)
            {
                return null;
            }
            Debug.Assert(removedKey == key);

            RemoveShadowPropertiesIfUnused(key.Properties);
            foreach (var property in key.Properties)
            {
                if (property.ClrType.IsNullableType())
                {
                    property.Builder?.IsRequired(false, configurationSource);
                }
            }

            return currentConfigurationSource;
        }

        public virtual InternalPropertyBuilder Property(
            [NotNull] string propertyName, [NotNull] Type propertyType, ConfigurationSource configurationSource)
            => Property(propertyName, propertyType, /*shadowProperty:*/ null, configurationSource);

        public virtual InternalPropertyBuilder Property([NotNull] string propertyName, ConfigurationSource configurationSource)
            => Property(propertyName, null, /*shadowProperty:*/ null, configurationSource);

        public virtual InternalPropertyBuilder Property([NotNull] PropertyInfo clrProperty, ConfigurationSource configurationSource)
            => Property(clrProperty.Name, clrProperty.PropertyType, /*shadowProperty:*/ false, configurationSource);

        private InternalPropertyBuilder Property(
            string propertyName, Type propertyType, bool? shadowProperty, ConfigurationSource configurationSource)
        {
            if (IsIgnored(propertyName, configurationSource))
            {
                return null;
            }

            PropertyBuildersSnapshot detachedProperties = null;
            var existingProperty = Metadata.FindProperty(propertyName);
            if (existingProperty == null)
            {
                var derivedProperties = Metadata.FindDerivedProperties(propertyName);
                detachedProperties = DetachProperties(derivedProperties);
            }
            else if (existingProperty.DeclaringEntityType != Metadata)
            {
                return existingProperty.DeclaringEntityType.Builder
                    .Property(existingProperty, propertyName, propertyType, shadowProperty, configurationSource);
            }

            var builder = Property(existingProperty, propertyName, propertyType, shadowProperty, configurationSource);

            detachedProperties?.Attach(this);

            return builder;
        }

        private InternalPropertyBuilder Property(
            Property existingProperty,
            string propertyName,
            Type propertyType,
            bool? shadowProperty,
            ConfigurationSource configurationSource)
        {
            var property = existingProperty;
            if (existingProperty == null)
            {
                Unignore(propertyName);

                property = Metadata.AddProperty(propertyName, configurationSource, runConventions: false);
            }
            else
            {
                property.UpdateConfigurationSource(configurationSource);
            }

            var builder = ConfigureProperty(property.Builder, propertyType, shadowProperty, configurationSource);

            if (existingProperty == null)
            {
                builder = Metadata.Model.ConventionDispatcher.OnPropertyAdded(property.Builder);
            }

            return builder;
        }

        private static InternalPropertyBuilder ConfigureProperty(
            InternalPropertyBuilder builder, Type propertyType, bool? shadowProperty, ConfigurationSource configurationSource)
        {
            if (builder == null)
            {
                return null;
            }

            if ((propertyType != null)
                && !builder.HasClrType(propertyType, configurationSource))
            {
                return null;
            }

            if (shadowProperty.HasValue
                && !builder.IsShadow(shadowProperty.Value, configurationSource))
            {
                return null;
            }

            return builder;
        }

        private bool CanRemoveProperty(
            [NotNull] Property property, ConfigurationSource configurationSource, bool canOverrideSameSource = true)
        {
            Check.NotNull(property, nameof(property));
            Debug.Assert(property.DeclaringEntityType == Metadata);

            var currentConfigurationSource = property.GetConfigurationSource();
            return configurationSource.Overrides(currentConfigurationSource)
                   && (canOverrideSameSource || (configurationSource != currentConfigurationSource));
        }

        public virtual bool CanAddNavigation([NotNull] string navigationName, ConfigurationSource configurationSource)
            => !IsIgnored(navigationName, configurationSource: configurationSource)
               && !Metadata.FindNavigationsInHierarchy(navigationName).Any();

        public virtual bool CanAddOrReplaceNavigation([NotNull] string navigationName, ConfigurationSource configurationSource)
            => !IsIgnored(navigationName, configurationSource: configurationSource)
               && Metadata.FindNavigationsInHierarchy(navigationName).All(n =>
                   n.ForeignKey.Builder.CanSetNavigation(null, n.IsDependentToPrincipal(), configurationSource));

        public virtual bool IsIgnored([NotNull] string name, ConfigurationSource configurationSource)
        {
            Check.NotEmpty(name, nameof(name));

            if (configurationSource == ConfigurationSource.Explicit)
            {
                return false;
            }

            var ignoredConfigurationSource = Metadata.FindIgnoredMemberConfigurationSource(name);
            if (ignoredConfigurationSource.HasValue
                && ignoredConfigurationSource.Value.Overrides(configurationSource))
            {
                return true;
            }

            if (Metadata.BaseType != null)
            {
                return Metadata.BaseType.Builder.IsIgnored(name, configurationSource);
            }

            return false;
        }

        public virtual bool CanRemoveForeignKey([NotNull] ForeignKey foreignKey, ConfigurationSource configurationSource)
        {
            Debug.Assert(foreignKey.DeclaringEntityType == Metadata);

            var currentConfigurationSource = foreignKey.GetConfigurationSource();
            return configurationSource.Overrides(currentConfigurationSource);
        }

        public virtual bool Ignore([NotNull] string name, ConfigurationSource configurationSource)
        {
            var ignoredConfigurationSource = Metadata.FindIgnoredMemberConfigurationSource(name);
            if (ignoredConfigurationSource.HasValue)
            {
                if (configurationSource.Overrides(ignoredConfigurationSource)
                    && (configurationSource != ignoredConfigurationSource))
                {
                    Metadata.Ignore(name, configurationSource);
                }
                return true;
            }

            Metadata.Ignore(name, configurationSource);
            var navigation = Metadata.FindNavigation(name);
            if (navigation != null)
            {
                var foreignKey = navigation.ForeignKey;
                if (foreignKey.DeclaringEntityType.Builder.RemoveForeignKey(foreignKey, configurationSource) == null)
                {
                    Metadata.Unignore(name);
                    return false;
                }
            }
            else
            {
                var property = Metadata.FindProperty(name);
                if ((property != null)
                    && (property.DeclaringEntityType.Builder.RemoveProperty(property, configurationSource) == null))
                {
                    Metadata.Unignore(name);
                    return false;
                }
            }

            return true;
        }

        public virtual void Unignore([NotNull] string memberName)
        {
            var entityType = Metadata;
            foreach (var derivedType in entityType.GetDerivedTypes())
            {
                derivedType.Unignore(memberName);
            }

            while (entityType != null)
            {
                entityType.Unignore(memberName);
                entityType = entityType.BaseType;
            }
        }

        public virtual InternalEntityTypeBuilder HasBaseType([CanBeNull] Type baseEntityType, ConfigurationSource configurationSource)
        {
            if (baseEntityType == null)
            {
                return HasBaseType((EntityType)null, configurationSource);
            }

            var baseType = ModelBuilder.Entity(baseEntityType, configurationSource);
            return baseType == null
                ? null
                : HasBaseType(baseType.Metadata, configurationSource);
        }

        public virtual InternalEntityTypeBuilder HasBaseType([CanBeNull] string baseEntityTypeName, ConfigurationSource configurationSource)
        {
            if (baseEntityTypeName == null)
            {
                return HasBaseType((EntityType)null, configurationSource);
            }

            var baseType = ModelBuilder.Entity(baseEntityTypeName, configurationSource);
            return baseType == null
                ? null
                : HasBaseType(baseType.Metadata, configurationSource);
        }

        public virtual InternalEntityTypeBuilder HasBaseType([CanBeNull] EntityType baseEntityType, ConfigurationSource configurationSource)
        {
            if (Metadata.BaseType == baseEntityType)
            {
                Metadata.HasBaseType(baseEntityType, configurationSource);
                return this;
            }

            if (!configurationSource.Overrides(Metadata.GetBaseTypeConfigurationSource()))
            {
                return null;
            }

            var detachedRelationships = new HashSet<RelationshipBuilderSnapshot>();
            PropertyBuildersSnapshot detachedProperties = null;
            var removedKeys = new List<Key>();
            var changedRelationships = new List<InternalRelationshipBuilder>();
            IReadOnlyList<RelationshipSnapshot> relationshipsToBeRemoved = new List<RelationshipSnapshot>();
            if (baseEntityType != null)
            {
                if (Metadata.GetDeclaredKeys().Any(k => !configurationSource.Overrides(k.GetConfigurationSource())))
                {
                    return null;
                }

                relationshipsToBeRemoved = FindConflictingRelationships(baseEntityType, configurationSource);
                if (relationshipsToBeRemoved == null)
                {
                    return null;
                }

                var foreignKeysUsingKeyProperties = Metadata.GetDeclaredForeignKeys()
                    .Where(fk => relationshipsToBeRemoved.All(r => r.ForeignKey != fk)
                                 && fk.Properties.Any(p => baseEntityType.FindProperty(p.Name)?.IsKey() == true)).ToList();

                if (foreignKeysUsingKeyProperties.Any(fk =>
                    !configurationSource.Overrides(fk.GetForeignKeyPropertiesConfigurationSource())))
                {
                    return null;
                }

                changedRelationships.AddRange(
                    foreignKeysUsingKeyProperties.Select(foreignKeyUsingKeyProperties =>
                        foreignKeyUsingKeyProperties.Builder.HasForeignKey(null, configurationSource, runConventions: false)));

                foreach (var relationshipToBeRemoved in relationshipsToBeRemoved)
                {
                    var removedConfigurationSource = relationshipToBeRemoved.ForeignKey.DeclaringEntityType.Builder
                        .RemoveForeignKey(relationshipToBeRemoved.ForeignKey, configurationSource, runConventions: false);
                    Debug.Assert(removedConfigurationSource.HasValue);
                }

                foreach (var key in Metadata.GetDeclaredKeys().ToList())
                {
                    foreach (var referencingForeignKey in key.FindReferencingForeignKeys().ToList())
                    {
                        detachedRelationships.Add(DetachRelationship(referencingForeignKey));
                    }
                }

                // TODO: Detach and reattach keys
                // Issue #2611
                removedKeys = Metadata.GetDeclaredKeys().ToList();
                foreach (var key in removedKeys)
                {
                    var removedConfigurationSource = RemoveKey(key, configurationSource, runConventions: false);
                    Debug.Assert(removedConfigurationSource.HasValue);
                }

                var duplicatedProperties = baseEntityType.GetProperties()
                    .Select(p => Metadata.FindDeclaredProperty(p.Name))
                    .Where(p => p != null);

                // TODO: Detach base property if shadow and derived non-shadow
                detachedProperties = DetachProperties(duplicatedProperties);

                baseEntityType.UpdateConfigurationSource(configurationSource);
            }

            var originalBaseType = Metadata.BaseType;
            Metadata.HasBaseType(baseEntityType, configurationSource, runConventions: false);

            detachedProperties?.Attach(this);

            foreach (var detachedRelationship in detachedRelationships)
            {
                detachedRelationship.Attach();
            }

            foreach (var changedRelationship in changedRelationships)
            {
                ModelBuilder.Metadata.ConventionDispatcher.OnForeignKeyAdded(changedRelationship);
            }

            foreach (var relationshipToBeRemoved in relationshipsToBeRemoved)
            {
                var dependentEntityType = relationshipToBeRemoved.ForeignKey.DeclaringEntityType.Builder;
                var principalEntityType = relationshipToBeRemoved.ForeignKey.PrincipalEntityType.Builder;
                var source = relationshipToBeRemoved.IsDependent ? dependentEntityType : principalEntityType;
                var target = relationshipToBeRemoved.IsDependent ? principalEntityType : dependentEntityType;

                if (relationshipToBeRemoved.NavigationFrom != null)
                {
                    ModelBuilder.Metadata.ConventionDispatcher.OnNavigationRemoved(source, target, relationshipToBeRemoved.NavigationFrom.Name);
                }
                if (relationshipToBeRemoved.NavigationTo != null)
                {
                    ModelBuilder.Metadata.ConventionDispatcher.OnNavigationRemoved(target, source, relationshipToBeRemoved.NavigationTo.Name);
                }

                ModelBuilder.Metadata.ConventionDispatcher.OnForeignKeyRemoved(dependentEntityType, relationshipToBeRemoved.ForeignKey);
            }

            foreach (var removedKey in removedKeys)
            {
                ModelBuilder.Metadata.ConventionDispatcher.OnKeyRemoved(this, removedKey);
            }

            ModelBuilder.Metadata.ConventionDispatcher.OnBaseEntityTypeSet(this, originalBaseType);

            return this;
        }

        private PropertyBuildersSnapshot DetachProperties(IEnumerable<Property> propertiesToDetach)
        {
            var propertiesToDetachList = propertiesToDetach.ToList();
            if (propertiesToDetachList.Count == 0)
            {
                return null;
            }

            var detachedRelationships = new List<RelationshipBuilderSnapshot>();
            foreach (var propertyToDetach in propertiesToDetachList)
            {
                foreach (var relationship in propertyToDetach.FindContainingForeignKeys().ToList())
                {
                    detachedRelationships.Add(DetachRelationship(relationship));
                }
            }

            // TODO: Detach and reattach keys and the referencing FKs
            // Issue #2611

            var detachedIndexes = new List<IndexBuildersSnapshot>();
            foreach (var propertyToDetach in propertiesToDetachList)
            {
                var indexesToDetach = propertyToDetach.FindContainingIndexes().ToList();
                if (indexesToDetach.Count > 0)
                {
                    detachedIndexes.Add(DetachIndexes(indexesToDetach.OfType<Index>()));
                }
            }

            var detachedProperties = new List<Tuple<InternalPropertyBuilder, ConfigurationSource>>();
            foreach (var propertyToDetach in propertiesToDetachList)
            {
                var property = propertyToDetach.DeclaringEntityType.FindDeclaredProperty(propertyToDetach.Name);
                if (property != null)
                {
                    var entityTypeBuilder = propertyToDetach.DeclaringEntityType.Builder;
                    var propertyBuilder = propertyToDetach.Builder;
                    var removedConfigurationSource = entityTypeBuilder
                        .RemoveProperty(propertyToDetach, ConfigurationSource.Explicit);
                    detachedProperties.Add(Tuple.Create(propertyBuilder, removedConfigurationSource.Value));
                }
            }

            return new PropertyBuildersSnapshot(detachedProperties, detachedIndexes, detachedRelationships);
        }

        private class PropertyBuildersSnapshot
        {
            public PropertyBuildersSnapshot(
                IReadOnlyList<Tuple<InternalPropertyBuilder, ConfigurationSource>> properties,
                IReadOnlyList<IndexBuildersSnapshot> indexes,
                IReadOnlyList<RelationshipBuilderSnapshot> relationships)
            {
                Properties = properties;
                Indexes = indexes;
                Relationships = relationships;
            }

            private IReadOnlyList<Tuple<InternalPropertyBuilder, ConfigurationSource>> Properties { get; }
            private IReadOnlyList<RelationshipBuilderSnapshot> Relationships { get; }
            private IReadOnlyList<IndexBuildersSnapshot> Indexes { get; }

            public void Attach(InternalEntityTypeBuilder entityTypeBuilder)
            {
                foreach (var propertyTuple in Properties)
                {
                    propertyTuple.Item1.Attach(entityTypeBuilder, propertyTuple.Item2);
                }

                foreach (var detachedIndexes in Indexes)
                {
                    detachedIndexes.Attach();
                }

                foreach (var detachedRelationship in Relationships)
                {
                    detachedRelationship.Attach();
                }
            }
        }

        private IReadOnlyList<RelationshipSnapshot> FindConflictingRelationships(
            EntityType baseEntityType,
            ConfigurationSource configurationSource)
        {
            var relationshipsToBeRemoved = new List<RelationshipSnapshot>();
            var baseRelationshipsByTargetType = GroupRelationshipsByTargetType(baseEntityType);
            var relationshipsByTargetType = GroupRelationshipsByTargetType(Metadata);

            foreach (var relatedEntityType in relationshipsByTargetType.Keys)
            {
                if (!baseRelationshipsByTargetType.ContainsKey(relatedEntityType))
                {
                    continue;
                }

                foreach (var baseRelationship in baseRelationshipsByTargetType[relatedEntityType])
                {
                    foreach (var relationship in relationshipsByTargetType[relatedEntityType])
                    {
                        if ((baseRelationship.IsDependent
                             && relationship.IsDependent
                             && PropertyListComparer.Instance.Equals(
                                 baseRelationship.ForeignKey.Properties,
                                 relationship.ForeignKey.Properties))
                            || ((relationship.NavigationFrom != null)
                                && (baseRelationship.NavigationFrom?.Name == relationship.NavigationFrom.Name)))
                        {
                            if (relationship.ForeignKey.DeclaringEntityType.Builder
                                .CanRemoveForeignKey(relationship.ForeignKey, configurationSource))
                            {
                                relationshipsToBeRemoved.Add(relationship);
                            }
                            else if (baseRelationship.ForeignKey.DeclaringEntityType.Builder
                                .CanRemoveForeignKey(baseRelationship.ForeignKey, configurationSource))
                            {
                                relationshipsToBeRemoved.Add(baseRelationship);
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                }
            }

            return relationshipsToBeRemoved;
        }

        private static Dictionary<EntityType, List<RelationshipSnapshot>> GroupRelationshipsByTargetType(EntityType entityType)
            => entityType.GetForeignKeys()
                .Select(foreignKey =>
                    new RelationshipSnapshot(foreignKey,
                        foreignKey.DependentToPrincipal,
                        foreignKey.PrincipalToDependent,
                        isDependent: true))
                .Concat(entityType.GetReferencingForeignKeys().Where(foreignKey => !foreignKey.IsSelfReferencing())
                    .Select(foreignKey =>
                        new RelationshipSnapshot(foreignKey,
                            foreignKey.PrincipalToDependent,
                            foreignKey.DependentToPrincipal,
                            isDependent: false)))
                .GroupBy(relationship => relationship.IsDependent
                    ? relationship.ForeignKey.PrincipalEntityType
                    : relationship.ForeignKey.DeclaringEntityType)
                .ToDictionary(g => g.Key, g => g.ToList());

        private ConfigurationSource? RemoveProperty(
            Property property, ConfigurationSource configurationSource, bool canOverrideSameSource = true)
        {
            var currentConfigurationSource = property.GetConfigurationSource();
            if (!configurationSource.Overrides(currentConfigurationSource)
                || !(canOverrideSameSource || (configurationSource != currentConfigurationSource)))
            {
                return null;
            }

            var detachedRelationships = property.FindContainingForeignKeys().ToList()
                .Select(DetachRelationship).ToList();

            foreach (var key in Metadata.GetKeys().Where(i => i.Properties.Contains(property)).ToList())
            {
                detachedRelationships.AddRange(key.FindReferencingForeignKeys().ToList()
                    .Select(DetachRelationship));
                var removed = RemoveKey(key, configurationSource);
                Debug.Assert(removed.HasValue);
            }

            foreach (var index in Metadata.GetIndexes().Where(i => i.Properties.Contains(property)).ToList())
            {
                var removed = RemoveIndex(index, configurationSource);
                Debug.Assert(removed.HasValue);
            }

            if (Metadata.GetProperties().Contains(property))
            {
                var removedProperty = Metadata.RemoveProperty(property.Name);
                Debug.Assert(removedProperty == property);
            }

            foreach (var detachedRelationship in detachedRelationships)
            {
                detachedRelationship.Attach();
            }

            return currentConfigurationSource;
        }

        private RelationshipBuilderSnapshot DetachRelationship([NotNull] ForeignKey foreignKey)
        {
            var relationshipBuilder = foreignKey.Builder;
            var relationshipConfigurationSource = foreignKey.DeclaringEntityType.Builder
                .RemoveForeignKey(foreignKey, ConfigurationSource.Explicit, runConventions: false);
            Debug.Assert(relationshipConfigurationSource != null);

            return new RelationshipBuilderSnapshot(relationshipBuilder, relationshipConfigurationSource.Value);
        }

        public virtual ConfigurationSource? RemoveForeignKey([NotNull] ForeignKey foreignKey, ConfigurationSource configurationSource)
            => RemoveForeignKey(foreignKey, configurationSource, runConventions: true);

        public virtual ConfigurationSource? RemoveForeignKey([NotNull] ForeignKey foreignKey, ConfigurationSource configurationSource, bool runConventions)
        {
            Debug.Assert(foreignKey.DeclaringEntityType == Metadata);

            var currentConfigurationSource = foreignKey.GetConfigurationSource();
            if (!configurationSource.Overrides(currentConfigurationSource))
            {
                return null;
            }

            var removedForeignKey = Metadata.RemoveForeignKey(
                foreignKey.Properties, foreignKey.PrincipalKey, foreignKey.PrincipalEntityType, runConventions);

            if (removedForeignKey == null)
            {
                return null;
            }
            Debug.Assert(removedForeignKey == foreignKey);

            var index = Metadata.FindIndex(foreignKey.Properties);
            if (index != null
                && !index.IsInUse())
            {
                // Remove index if created by convention
                index.DeclaringEntityType.Builder.RemoveIndex(index, ConfigurationSource.Convention);
            }

            RemoveShadowPropertiesIfUnused(foreignKey.Properties.Where(p => p.DeclaringEntityType.FindDeclaredProperty(p.Name) != null).ToList());
            foreignKey.PrincipalKey.DeclaringEntityType.Builder?.RemoveKeyIfUnused(foreignKey.PrincipalKey);

            return currentConfigurationSource;
        }

        private void RemoveKeyIfUnused(Key key)
        {
            if (Metadata.FindPrimaryKey() == key)
            {
                return;
            }

            if (key.FindReferencingForeignKeys().Any())
            {
                return;
            }

            RemoveKey(key, ConfigurationSource.Convention);
        }

        public virtual void RemoveShadowPropertiesIfUnused([NotNull] IReadOnlyList<Property> properties)
        {
            foreach (var property in properties.ToList())
            {
                if (property.IsShadowProperty)
                {
                    RemovePropertyIfUnused(property);
                }
            }
        }

        private void RemovePropertyIfUnused(Property property)
        {
            if (!property.DeclaringEntityType.Builder.CanRemoveProperty(property, ConfigurationSource.Convention))
            {
                return;
            }

            if (property.FindContainingIndexes().Any())
            {
                return;
            }

            if (property.FindContainingForeignKeys().Any())
            {
                return;
            }

            if (property.FindContainingKeys().Any())
            {
                return;
            }

            var removedProperty = property.DeclaringEntityType.RemoveProperty(property.Name);
            Debug.Assert(removedProperty == property);
        }

        public virtual InternalIndexBuilder HasIndex([NotNull] IReadOnlyList<string> propertyNames, ConfigurationSource configurationSource)
            => HasIndex(GetOrCreateProperties(propertyNames, configurationSource), configurationSource);

        public virtual InternalIndexBuilder HasIndex([NotNull] IReadOnlyList<PropertyInfo> clrProperties, ConfigurationSource configurationSource)
            => HasIndex(GetOrCreateProperties(clrProperties, configurationSource), configurationSource);

        private InternalIndexBuilder HasIndex(IReadOnlyList<Property> properties, ConfigurationSource configurationSource)
        {
            if (properties == null)
            {
                return null;
            }

            IndexBuildersSnapshot detachedIndexes = null;
            var existingIndex = Metadata.FindIndex(properties);
            if (existingIndex == null)
            {
                var derivedIndexes = Metadata.FindDerivedIndexes(properties);
                detachedIndexes = DetachIndexes(derivedIndexes);
            }
            else if (existingIndex.DeclaringEntityType != Metadata)
            {
                return existingIndex.DeclaringEntityType.Builder.HasIndex(existingIndex, properties, configurationSource);
            }

            var indexBuilder = HasIndex(existingIndex, properties, configurationSource);

            detachedIndexes?.Attach();

            return indexBuilder;
        }

        private InternalIndexBuilder HasIndex(
            Index existingIndex, IReadOnlyList<Property> properties, ConfigurationSource configurationSource)
        {
            if (existingIndex == null)
            {
                var index = Metadata.AddIndex(properties, configurationSource);
                return index.Builder;
            }

            existingIndex.UpdateConfigurationSource(configurationSource);
            return existingIndex.Builder;
        }

        public virtual ConfigurationSource? RemoveIndex([NotNull] Index index, ConfigurationSource configurationSource)
        {
            var currentConfigurationSource = index.GetConfigurationSource();
            if (!configurationSource.Overrides(currentConfigurationSource))
            {
                return null;
            }

            var removedIndex = Metadata.RemoveIndex(index.Properties);
            Debug.Assert(removedIndex == index);

            RemoveShadowPropertiesIfUnused(index.Properties);

            return currentConfigurationSource;
        }

        private class IndexBuildersSnapshot
        {
            public IndexBuildersSnapshot(IReadOnlyList<Tuple<InternalIndexBuilder, ConfigurationSource>> indexes)
            {
                Indexes = indexes;
            }

            private IReadOnlyList<Tuple<InternalIndexBuilder, ConfigurationSource>> Indexes { get; }

            public void Attach()
            {
                foreach (var indexTuple in Indexes)
                {
                    indexTuple.Item1.Attach(indexTuple.Item2);
                }
            }
        }

        private static IndexBuildersSnapshot DetachIndexes(IEnumerable<Index> indexesToDetach)
        {
            var indexesToDetachList = indexesToDetach.ToList();
            if (indexesToDetachList.Count == 0)
            {
                return null;
            }

            var detachedIndexes = new List<Tuple<InternalIndexBuilder, ConfigurationSource>>();
            foreach (var indexToDetach in indexesToDetachList)
            {
                var entityTypeBuilder = indexToDetach.DeclaringEntityType.Builder;
                var indexBuilder = entityTypeBuilder.HasIndex(indexToDetach.Properties, ConfigurationSource.Convention);
                var removedConfigurationSource = entityTypeBuilder.RemoveIndex(indexToDetach, ConfigurationSource.Explicit);
                Debug.Assert(removedConfigurationSource != null);

                detachedIndexes.Add(Tuple.Create(indexBuilder, removedConfigurationSource.Value));
            }

            return new IndexBuildersSnapshot(detachedIndexes);
        }

        public virtual InternalRelationshipBuilder HasForeignKey(
            [NotNull] string principalEntityTypeName,
            [NotNull] IReadOnlyList<string> propertyNames,
            ConfigurationSource configurationSource)
        {
            Check.NotEmpty(principalEntityTypeName, nameof(principalEntityTypeName));
            Check.NotEmpty(propertyNames, nameof(propertyNames));

            var principalType = ModelBuilder.Entity(principalEntityTypeName, configurationSource);
            return principalType == null
                ? null
                : HasForeignKeyInternal(
                    principalType,
                    GetOrCreateProperties(propertyNames, configurationSource, principalType.Metadata.FindPrimaryKey()?.Properties),
                    configurationSource);
        }

        public virtual InternalRelationshipBuilder HasForeignKey(
            [NotNull] Type principalClrType,
            [NotNull] IReadOnlyList<PropertyInfo> clrProperties,
            ConfigurationSource configurationSource)
        {
            Check.NotNull(principalClrType, nameof(principalClrType));
            Check.NotEmpty(clrProperties, nameof(clrProperties));

            var principalType = ModelBuilder.Entity(principalClrType, configurationSource);
            return principalType == null
                ? null
                : HasForeignKeyInternal(principalType, GetOrCreateProperties(clrProperties, configurationSource), configurationSource);
        }

        public virtual InternalRelationshipBuilder HasForeignKey(
            [NotNull] InternalEntityTypeBuilder principalEntityTypeBuilder,
            [NotNull] IReadOnlyList<Property> dependentProperties,
            ConfigurationSource configurationSource)
            => HasForeignKeyInternal(principalEntityTypeBuilder,
                GetActualProperties(dependentProperties, configurationSource),
                configurationSource);

        private InternalRelationshipBuilder HasForeignKeyInternal(
            [NotNull] InternalEntityTypeBuilder principalEntityTypeBuilder,
            [CanBeNull] IReadOnlyList<Property> dependentProperties,
            ConfigurationSource configurationSource)
        {
            if (dependentProperties == null)
            {
                return null;
            }

            InternalRelationshipBuilder relationship;
            InternalRelationshipBuilder newRelationship = null;
            RelationshipBuilderSnapshot detachedRelationship = null;
            var existingForeignKey = Metadata.FindForeignKeys(dependentProperties).FirstOrDefault();
            if (existingForeignKey == null)
            {
                var derivedForeignKey = Metadata.FindDerivedForeignKeys(dependentProperties).FirstOrDefault();
                if (derivedForeignKey != null)
                {
                    detachedRelationship = DetachRelationship(derivedForeignKey);
                }
                newRelationship = Relationship(principalEntityTypeBuilder, configurationSource);
                relationship = newRelationship;
            }
            else
            {
                relationship = existingForeignKey.Builder;
                existingForeignKey.UpdateConfigurationSource(configurationSource);
            }

            relationship = relationship.HasForeignKey(dependentProperties, configurationSource);
            if (relationship == null
                && newRelationship != null)
            {
                RemoveForeignKey(newRelationship.Metadata, configurationSource);
            }

            detachedRelationship?.Attach();

            return relationship;
        }

        public virtual IReadOnlyList<InternalRelationshipBuilder> GetRelationshipBuilders(
            [NotNull] EntityType principalEntityType,
            [CanBeNull] string navigationToPrincipalName,
            [CanBeNull] string navigationToDependentName,
            [CanBeNull] IReadOnlyList<Property> dependentProperties)
        {
            var existingRelationships = new List<InternalRelationshipBuilder>();
            if (!string.IsNullOrEmpty(navigationToPrincipalName))
            {
                existingRelationships.AddRange(Metadata
                    .FindNavigationsInHierarchy(navigationToPrincipalName)
                    .Select(n => n.ForeignKey.Builder));
            }

            if (!string.IsNullOrEmpty(navigationToDependentName))
            {
                existingRelationships.AddRange(principalEntityType
                    .FindNavigationsInHierarchy(navigationToDependentName)
                    .Select(n => n.ForeignKey.Builder));
            }

            if (dependentProperties != null)
            {
                existingRelationships.AddRange(Metadata
                    .FindForeignKeysInHierarchy(dependentProperties)
                    .Select(fk => fk.Builder));
            }

            return existingRelationships;
        }

        private InternalRelationshipBuilder CreateRelationshipBuilder(
            EntityType principalType,
            IReadOnlyList<Property> dependentProperties,
            Key principalKey,
            ConfigurationSource configurationSource,
            bool runConventions)
        {
            var key = Metadata.AddForeignKey(dependentProperties, principalKey, principalType, configurationSource: null, runConventions: false);
            key.UpdateConfigurationSource(configurationSource);
            principalType.UpdateConfigurationSource(configurationSource);

            HasIndex(dependentProperties, ConfigurationSource.Convention);

            var value = key.Builder;
            if (runConventions)
            {
                value = ModelBuilder.Metadata.ConventionDispatcher.OnForeignKeyAdded(value);
            }

            return value;
        }

        public virtual InternalRelationshipBuilder Relationship(
            [NotNull] InternalEntityTypeBuilder targetEntityTypeBuilder,
            [CanBeNull] string navigationToTargetName,
            [CanBeNull] string inverseNavigationName,
            ConfigurationSource configurationSource)
        {
            PropertyInfo navigationToTarget = null;
            if (navigationToTargetName != null)
            {
                if (!Navigation.IsCompatible(navigationToTargetName, Metadata, targetEntityTypeBuilder.Metadata, shouldBeCollection: null, shouldThrow: true))
                {
                    return null;
                }
                navigationToTarget = Metadata.ClrType.GetPropertiesInHierarchy(navigationToTargetName).First();
            }

            PropertyInfo inverseNavigation = null;
            if (inverseNavigationName != null)
            {
                if (!Navigation.IsCompatible(inverseNavigationName, targetEntityTypeBuilder.Metadata, Metadata, shouldBeCollection: null, shouldThrow: true))
                {
                    return null;
                }
                inverseNavigation = targetEntityTypeBuilder.Metadata.ClrType.GetPropertiesInHierarchy(inverseNavigationName).First();
            }

            return Relationship(targetEntityTypeBuilder, navigationToTarget, inverseNavigation, configurationSource);
        }

        public virtual InternalRelationshipBuilder Relationship(
            [NotNull] InternalEntityTypeBuilder targetEntityTypeBuilder,
            [CanBeNull] PropertyInfo navigationToTarget,
            [CanBeNull] PropertyInfo inverseNavigation,
            ConfigurationSource configurationSource)
        {
            Check.NotNull(targetEntityTypeBuilder, nameof(targetEntityTypeBuilder));

            if (inverseNavigation == null)
            {
                if (navigationToTarget == null)
                {
                    return Relationship(targetEntityTypeBuilder, configurationSource)
                        .Navigations(null, null, configurationSource);
                }

                return targetEntityTypeBuilder.Relationship(
                    this,
                    inverseNavigation,
                    navigationToTarget,
                    configurationSource);
            }

            var toTargetCanBeUnique = Navigation.IsCompatible(
                inverseNavigation.Name, targetEntityTypeBuilder.Metadata, Metadata, shouldBeCollection: false, shouldThrow: false);
            var toTargetCanBeNonUnique = Navigation.IsCompatible(
                inverseNavigation.Name, targetEntityTypeBuilder.Metadata, Metadata, shouldBeCollection: true, shouldThrow: false);
            if (!toTargetCanBeUnique
                && !toTargetCanBeNonUnique)
            {
                return null;
            }

            if (navigationToTarget == null)
            {
                if (!toTargetCanBeUnique)
                {
                    return Navigations(
                        Relationship(targetEntityTypeBuilder, configurationSource)
                            .PrincipalEntityType(targetEntityTypeBuilder, configurationSource)
                            .IsUnique(false, configurationSource),
                        null,
                        inverseNavigation.Name,
                        configurationSource);
                }

                return targetEntityTypeBuilder.Navigations(
                    targetEntityTypeBuilder.Relationship(this, configurationSource),
                    inverseNavigation.Name,
                    null,
                    configurationSource);
            }

            var toSourceCanBeUnique = Navigation.IsCompatible(
                navigationToTarget.Name, Metadata, targetEntityTypeBuilder.Metadata, shouldBeCollection: false, shouldThrow: false);
            var toSourceCanBeNonUnique = Navigation.IsCompatible(
                navigationToTarget.Name, Metadata, targetEntityTypeBuilder.Metadata, shouldBeCollection: true, shouldThrow: false);
            if (!toSourceCanBeUnique
                && !toSourceCanBeNonUnique)
            {
                return null;
            }

            if (!toTargetCanBeUnique)
            {
                if (!toSourceCanBeUnique)
                {
                    // TODO: Support many to many
                    return null;
                }

                return Navigations(
                    Relationship(targetEntityTypeBuilder, configurationSource)
                        .PrincipalEntityType(targetEntityTypeBuilder, configurationSource)
                        .IsUnique(false, configurationSource),
                    navigationToTarget.Name,
                    inverseNavigation.Name,
                    configurationSource);
            }

            if (!toSourceCanBeUnique)
            {
                return targetEntityTypeBuilder.Navigations(
                    targetEntityTypeBuilder.Relationship(this, configurationSource)
                        .PrincipalEntityType(this, configurationSource)
                        .IsUnique(false, configurationSource),
                    inverseNavigation.Name,
                    navigationToTarget.Name,
                    configurationSource);
            }

            var relationship = Relationship(targetEntityTypeBuilder, configurationSource);
            if (!toTargetCanBeNonUnique
                && !toSourceCanBeNonUnique)
            {
                relationship = relationship.IsUnique(true, configurationSource);
            }
            else
            {
                relationship = relationship.IsUnique(true, ConfigurationSource.Convention);
            }

            return Navigations(relationship, navigationToTarget.Name, inverseNavigation.Name, configurationSource);
        }

        private InternalRelationshipBuilder Navigations(
            InternalRelationshipBuilder relationship,
            string navigationToPrincipalName,
            string navigationToDependentName,
            ConfigurationSource configurationSource)
        {
            var relationshipWithNavigations = relationship.Navigations(navigationToPrincipalName, navigationToDependentName, configurationSource);
            if (relationshipWithNavigations == null
                && relationship.Metadata.Builder != null)
            {
                RemoveForeignKey(relationship.Metadata, configurationSource);
            }

            return relationshipWithNavigations;
        }

        public virtual InternalRelationshipBuilder Relationship(
            [NotNull] EntityType principalEntityType,
            ConfigurationSource configurationSource)
            => Relationship(principalEntityType.Builder, configurationSource);

        public virtual InternalRelationshipBuilder Relationship(
            [NotNull] InternalEntityTypeBuilder principalEntityTypeBuilder,
            ConfigurationSource configurationSource)
            => CreateForeignKey(
                principalEntityTypeBuilder,
                null,
                null,
                null,
                null,
                configurationSource,
                runConventions: true);

        public virtual InternalRelationshipBuilder CreateForeignKey(
            [NotNull] InternalEntityTypeBuilder principalEntityTypeBuilder,
            [CanBeNull] IReadOnlyList<Property> dependentProperties,
            [CanBeNull] IReadOnlyList<Property> principalProperties,
            [CanBeNull] string navigationToPrincipalName,
            bool? isRequired,
            ConfigurationSource configurationSource,
            bool runConventions)
        {
            var principalType = principalEntityTypeBuilder.Metadata;
            Debug.Assert((dependentProperties == null)
                         || Metadata.FindForeignKeys(dependentProperties)
                             .All(foreignKey => foreignKey.PrincipalEntityType != principalEntityTypeBuilder.Metadata));

            var principalBaseEntityTypeBuilder = principalType.RootType().Builder;
            Key principalKey;
            if (principalProperties != null)
            {
                var keyBuilder = principalBaseEntityTypeBuilder.HasKey(principalProperties, ConfigurationSource.Convention);
                principalKey = keyBuilder.Metadata;
            }
            else
            {
                principalKey = principalType.FindPrimaryKey();
            }

            if (dependentProperties != null)
            {
                dependentProperties = GetActualProperties(dependentProperties, ConfigurationSource.Convention);
                if ((principalKey == null)
                    || !ForeignKey.AreCompatible(
                        principalKey.Properties,
                        dependentProperties,
                        principalType,
                        Metadata,
                        shouldThrow: false))
                {
                    var principalKeyProperties = new Property[dependentProperties.Count];
                    for (var i = 0; i < dependentProperties.Count; i++)
                    {
                        IProperty foreignKeyProperty = dependentProperties[i];
                        principalKeyProperties[i] = CreateUniqueProperty(
                            foreignKeyProperty.Name,
                            foreignKeyProperty.ClrType,
                            principalBaseEntityTypeBuilder,
                            isRequired: true);
                    }

                    var keyBuilder = principalBaseEntityTypeBuilder.HasKey(principalKeyProperties, ConfigurationSource.Convention);

                    principalKey = keyBuilder.Metadata;
                }
            }
            else
            {
                if (principalKey == null)
                {
                    var principalKeyProperty = CreateUniqueProperty(
                        "TempId",
                        typeof(int),
                        principalBaseEntityTypeBuilder,
                        isRequired: true);

                    principalKey = principalBaseEntityTypeBuilder.HasKey(new[] { principalKeyProperty }, ConfigurationSource.Convention).Metadata;
                }

                var baseName = string.IsNullOrEmpty(navigationToPrincipalName) ? principalType.DisplayName() : navigationToPrincipalName;
                var fkProperties = new Property[principalKey.Properties.Count];
                for (var i = 0; i < principalKey.Properties.Count; i++)
                {
                    IProperty keyProperty = principalKey.Properties[i];
                    fkProperties[i] = CreateUniqueProperty(
                        baseName + keyProperty.Name,
                        isRequired ?? false ? keyProperty.ClrType : keyProperty.ClrType.MakeNullable(),
                        this,
                        isRequired);
                }

                dependentProperties = fkProperties;
            }

            return CreateRelationshipBuilder(principalType, dependentProperties, principalKey, configurationSource, runConventions);
        }

        private static Property CreateUniqueProperty(string baseName, Type propertyType, InternalEntityTypeBuilder entityTypeBuilder, bool? isRequired = null)
        {
            var index = -1;
            while (true)
            {
                var name = baseName + (++index > 0 ? index.ToString() : "");
                var entityType = entityTypeBuilder.Metadata;
                if (entityType.FindPropertiesInHierarchy(name).Any()
                    || (entityType.ClrType?.GetRuntimeProperties().FirstOrDefault(p => p.Name == name) != null))
                {
                    continue;
                }

                var propertyBuilder = entityTypeBuilder.Property(name, propertyType, ConfigurationSource.Convention);
                if (propertyBuilder != null)
                {
                    if (isRequired.HasValue
                        && propertyType.IsNullableType())
                    {
                        propertyBuilder.IsRequired(isRequired.Value, ConfigurationSource.Convention);
                    }
                    return propertyBuilder.Metadata;
                }
            }
        }

        public virtual IReadOnlyList<Property> GetOrCreateProperties([CanBeNull] IEnumerable<string> propertyNames, ConfigurationSource configurationSource, [CanBeNull] IEnumerable<Property> referencedProperties = null)
        {
            if (propertyNames == null)
            {
                return null;
            }

            var list = new List<Property>();
            var propertyNamesList = propertyNames.ToList();
            var referencedPropertiesList = referencedProperties?.ToList();
            if (referencedPropertiesList != null
                && referencedPropertiesList.Count != propertyNamesList.Count)
            {
                referencedPropertiesList = null;
            }
            var typesList = referencedPropertiesList?.Select(p => p.IsShadowProperty ? null : p.ClrType).ToList();
            for (var i = 0; i < propertyNamesList.Count; i++)
            {
                var propertyName = propertyNamesList[i];
                var property = Metadata.FindProperty(propertyName);
                if (property == null)
                {
                    var clrProperty = Metadata.ClrType?.GetPropertiesInHierarchy(propertyName).FirstOrDefault();
                    var type = typesList?[i];
                    InternalPropertyBuilder propertyBuilder;
                    if (clrProperty != null)
                    {
                        propertyBuilder = Property(clrProperty, configurationSource);
                    }
                    else if (type != null)
                    {
                        // TODO: Log that shadow property is created by convention
                        propertyBuilder = Property(propertyName, type, ConfigurationSource.Convention);
                    }
                    else
                    {
                        throw new InvalidOperationException(CoreStrings.NoClrProperty(propertyName, Metadata.Name));
                    }

                    if (propertyBuilder == null)
                    {
                        return null;
                    }
                    property = propertyBuilder.Metadata;
                }
                else
                {
                    property.DeclaringEntityType.UpdateConfigurationSource(configurationSource);
                    property = property.DeclaringEntityType.Builder.Property(property.Name, configurationSource).Metadata;
                }
                list.Add(property);
            }
            return list;
        }

        public virtual IReadOnlyList<Property> GetOrCreateProperties([CanBeNull] IEnumerable<PropertyInfo> clrProperties, ConfigurationSource configurationSource)
        {
            if (clrProperties == null)
            {
                return null;
            }

            var list = new List<Property>();
            foreach (var propertyInfo in clrProperties)
            {
                var property = Metadata.FindProperty(propertyInfo);
                if (property == null)
                {
                    var propertyBuilder = Property(propertyInfo, configurationSource);
                    if (propertyBuilder == null)
                    {
                        return null;
                    }
                    property = propertyBuilder.Metadata;
                }

                list.Add(property);
            }
            return list;
        }

        private IReadOnlyList<Property> GetActualProperties(
            IEnumerable<Property> properties, ConfigurationSource configurationSource)
        {
            if (properties == null)
            {
                return null;
            }

            var actualProperties = new List<Property>();
            foreach (var property in properties)
            {
                var builder = property.Builder ?? Property(property.Name, configurationSource);
                if (builder == null)
                {
                    return null;
                }
                actualProperties.Add(builder.Metadata);
            }
            return actualProperties;
        }

        private struct RelationshipSnapshot
        {
            public readonly ForeignKey ForeignKey;
            public readonly Navigation NavigationFrom;
            public readonly Navigation NavigationTo;
            public readonly bool IsDependent;

            public RelationshipSnapshot(ForeignKey foreignKey, Navigation navigationFrom, Navigation navigationTo, bool isDependent)
            {
                ForeignKey = foreignKey;
                NavigationFrom = navigationFrom;
                NavigationTo = navigationTo;
                IsDependent = isDependent;
            }
        }

        private class RelationshipBuilderSnapshot
        {
            public RelationshipBuilderSnapshot(
                InternalRelationshipBuilder relationship,
                ConfigurationSource relationshipConfigurationSource)
            {
                Relationship = relationship;
                RelationshipConfigurationSource = relationshipConfigurationSource;
            }

            private InternalRelationshipBuilder Relationship { get; }
            private ConfigurationSource RelationshipConfigurationSource { get; }

            public InternalRelationshipBuilder Attach()
                => Relationship.Attach(RelationshipConfigurationSource);
        }
    }
}
