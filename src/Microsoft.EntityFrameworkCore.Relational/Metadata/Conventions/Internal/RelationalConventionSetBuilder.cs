// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal
{
    public abstract class RelationalConventionSetBuilder : IConventionSetBuilder
    {
        private readonly IRelationalTypeMapper _typeMapper;

        protected RelationalConventionSetBuilder([NotNull] IRelationalTypeMapper typeMapper)
        {
            Check.NotNull(typeMapper, nameof(typeMapper));

            _typeMapper = typeMapper;
        }

        public virtual ConventionSet AddConventions(ConventionSet conventionSet)
        {
            RelationshipDiscoveryConvention relationshipDiscoveryConvention = new RelationalRelationshipDiscoveryConvention(_typeMapper);

            ReplaceConvention(conventionSet.EntityTypeAddedConventions, (PropertyDiscoveryConvention)new RelationalPropertyDiscoveryConvention(_typeMapper));
            ReplaceConvention(conventionSet.EntityTypeAddedConventions, (InversePropertyAttributeConvention)new RelationalInversePropertyAttributeConvention(_typeMapper));
            ReplaceConvention(conventionSet.EntityTypeAddedConventions, relationshipDiscoveryConvention);

            ReplaceConvention(conventionSet.EntityTypeMemberIgnoredConventions, relationshipDiscoveryConvention);

            ReplaceConvention(conventionSet.ForeignKeyAddedConventions, (ForeignKeyAttributeConvention)new RelationalForeignKeyAttributeConvention(_typeMapper));

            ReplaceConvention(conventionSet.NavigationRemovedConventions, relationshipDiscoveryConvention);

            ReplaceConvention(conventionSet.ModelBuiltConventions, (PropertyMappingValidationConvention)new RelationalPropertyMappingValidationConvention(_typeMapper));

            conventionSet.PropertyAddedConventions.Add(new RelationalColumnAttributeConvention());

            conventionSet.EntityTypeAddedConventions.Add(new RelationalTableAttributeConvention());

            conventionSet.BaseEntityTypeSetConventions.Add(new DiscriminatorConvention());

            return conventionSet;
        }

        private static void ReplaceConvention<T1, T2>(IList<T1> conventionsList, T2 newConvention)
            where T2 : T1
        {
            var oldConvention = conventionsList.OfType<T2>().FirstOrDefault();
            if (oldConvention == null)
            {
                return;
            }
            var index = conventionsList.IndexOf(oldConvention);
            conventionsList.RemoveAt(index);
            conventionsList.Insert(index, newConvention);
        }
    }
}
