// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore.Internal
{
    /// <summary>
    ///     Represents categories of type arguments that may be used at runtime.
    ///     Runtime-directive scaffolding uses these categories to compute possible combinations of type arguments
    /// </summary>
    public static class TypeArgumentCategory
    {
        /// <summary>
        ///     All CLR types of keys. <see cref="IKey" />
        /// </summary>
        public static class Keys
        {
        }

        /// <summary>
        ///     All CLR types of entities. <see cref="IEntityType.ClrType" />
        /// </summary>
        public static class EntityTypes
        {
        }

        /// <summary>
        ///     All CLR types of properties <see cref="IProperty.ClrType" />
        /// </summary>
        public static class Properties
        {
        }

        /// <summary>
        ///     All common primitives.
        /// </summary>
        public static class Primitives
        {
        }

        /// <summary>
        ///     ICollection of <see cref="Primitives" />
        /// </summary>
        public static class PrimitiveCollections
        {
        }

        /// <summary>
        ///     All common generic collection types of entities. <seealso cref="ICollection{T}" />
        /// </summary>
        public static class EntityTypeCollections
        {
        }

        /// <summary>
        ///     All common LINQ grouping collections  <see cref="IGrouping{TKey,TElement}" />
        /// </summary>
        public static class EntityTypeGroupings
        {
        }

        /// <summary>
        ///     All CLR types of navigation properties <see cref="INavigation" />
        /// </summary>
        public static class NavigationProperties
        {
        }
    }
}
