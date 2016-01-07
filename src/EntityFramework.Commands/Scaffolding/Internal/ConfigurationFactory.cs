// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Metadata.Internal;
using Microsoft.Data.Entity.Scaffolding.Internal.Configuration;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Scaffolding.Internal
{
    public class ConfigurationFactory
    {
        public ConfigurationFactory([NotNull] IRelationalAnnotationProvider extensionsProvider,
            [NotNull] CSharpUtilities cSharpUtilities,
            [NotNull] ScaffoldingUtilities scaffoldingUtilities)
        {
            Check.NotNull(extensionsProvider, nameof(extensionsProvider));
            Check.NotNull(cSharpUtilities, nameof(cSharpUtilities));
            Check.NotNull(scaffoldingUtilities, nameof(scaffoldingUtilities));

            ExtensionsProvider = extensionsProvider;
            CSharpUtilities = cSharpUtilities;
            ScaffoldingUtilities = scaffoldingUtilities;
        }

        protected virtual IRelationalAnnotationProvider ExtensionsProvider { get;[param: NotNull] private set; }
        protected virtual CSharpUtilities CSharpUtilities { get;[param: NotNull] private set; }
        protected virtual ScaffoldingUtilities ScaffoldingUtilities { get;[param: NotNull] private set; }

        public virtual ModelConfiguration CreateModelConfiguration(
            [NotNull] IModel model,
            [NotNull] CustomConfiguration customConfiguration) 
            => new ModelConfiguration(this, model, customConfiguration, ExtensionsProvider, CSharpUtilities, ScaffoldingUtilities);

        public virtual CustomConfiguration CreateCustomConfiguration(
            [NotNull] string connectionString, [CanBeNull] string contextClassName,
            [NotNull] string @namespace, bool useFluentApiOnly)
        {
            return new CustomConfiguration(connectionString,
                contextClassName, @namespace, useFluentApiOnly);
        }

        public virtual OptionsBuilderConfiguration CreateOptionsBuilderConfiguration(
            [NotNull] ICollection<string> methodBodyLines)
        {
            Check.NotNull(methodBodyLines, nameof(methodBodyLines));

            return new OptionsBuilderConfiguration(methodBodyLines);
        }

        public virtual EntityConfiguration CreateEntityConfiguration(
            [NotNull] ModelConfiguration modelConfiguration,
            [NotNull] IEntityType entityType)
        {
            Check.NotNull(modelConfiguration, nameof(modelConfiguration));
            Check.NotNull(entityType, nameof(entityType));

            return new EntityConfiguration(modelConfiguration, entityType);
        }

        public virtual RelationshipConfiguration CreateRelationshipConfiguration(
            [NotNull] EntityConfiguration entityConfiguration,
            [NotNull] IForeignKey foreignKey,
            [NotNull] string dependentEndNavigationPropertyName,
            [NotNull] string principalEndNavigationPropertyName,
            DeleteBehavior onDeleteAction)
        {
            Check.NotNull(entityConfiguration, nameof(entityConfiguration));
            Check.NotNull(foreignKey, nameof(foreignKey));
            Check.NotEmpty(dependentEndNavigationPropertyName, nameof(dependentEndNavigationPropertyName));
            Check.NotEmpty(principalEndNavigationPropertyName, nameof(principalEndNavigationPropertyName));

            return new RelationshipConfiguration(
                entityConfiguration,
                foreignKey,
                dependentEndNavigationPropertyName,
                principalEndNavigationPropertyName,
                onDeleteAction);
        }

        public virtual PropertyConfiguration CreatePropertyConfiguration(
            [NotNull] EntityConfiguration entityConfiguration,
            [NotNull] IProperty property)
        {
            Check.NotNull(entityConfiguration, nameof(entityConfiguration));
            Check.NotNull(property, nameof(property));

            return new PropertyConfiguration(entityConfiguration, property);
        }

        public virtual NavigationPropertyConfiguration CreateNavigationPropertyConfiguration(
            [NotNull] string type, [NotNull] string name)
        {
            Check.NotEmpty(type, nameof(type));
            Check.NotEmpty(name, nameof(name));

            return new NavigationPropertyConfiguration(type, name);
        }

        public virtual NavigationPropertyInitializerConfiguration CreateNavigationPropertyInitializerConfiguration(
            [NotNull] string navPropName, [NotNull] string principalEntityTypeName)
        {
            Check.NotEmpty(navPropName, nameof(navPropName));
            Check.NotEmpty(principalEntityTypeName, nameof(principalEntityTypeName));

            return new NavigationPropertyInitializerConfiguration(navPropName, principalEntityTypeName);
        }

        public virtual FluentApiConfiguration CreateFluentApiConfiguration(
            bool attributeEquivalentExists,
            [NotNull] string methodName,
            [CanBeNull] params string[] methodArguments)
        {
            Check.NotEmpty(methodName, nameof(methodName));
            Check.NotNull(methodArguments, nameof(methodArguments));

            return new FluentApiConfiguration(methodName, methodArguments)
            {
                HasAttributeEquivalent = attributeEquivalentExists
            };
        }

        public virtual KeyFluentApiConfiguration CreateKeyFluentApiConfiguration(
            [NotNull] string lambdaIdentifier,
            [NotNull] Key key)
        {
            Check.NotEmpty(lambdaIdentifier, nameof(lambdaIdentifier));
            Check.NotNull(key, nameof(key));

            return new KeyFluentApiConfiguration(lambdaIdentifier, key);
        }

        public virtual AttributeConfiguration CreateAttributeConfiguration(
            [NotNull] string attributeName,
            [CanBeNull] params string[] attributeArguments)
        {
            Check.NotEmpty(attributeName, nameof(attributeName));
            Check.NotNull(attributeArguments, nameof(attributeArguments));

            return new AttributeConfiguration(attributeName, attributeArguments);
        }

        public virtual IndexConfiguration CreateIndexConfiguration(
            [NotNull] string lambdaIdentifier,
            [NotNull] Index index)
        {
            Check.NotEmpty(lambdaIdentifier, nameof(lambdaIdentifier));
            Check.NotNull(index, nameof(index));

            return new IndexConfiguration(lambdaIdentifier, index);
        }

        public virtual SequenceConfiguration CreateSequenceConfiguration()
            => new SequenceConfiguration();
    }
}
