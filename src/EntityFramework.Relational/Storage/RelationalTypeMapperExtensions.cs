// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Storage
{
    public static class RelationalTypeMapperExtensions
    {
        public static RelationalTypeMapping GetMappingForValue(
            [CanBeNull] this IRelationalTypeMapper typeMapper,
            [CanBeNull] object value)
            => (value == null)
               || (value == DBNull.Value)
               || (typeMapper == null)
                ? RelationalTypeMapping.NullMapping
                : typeMapper.GetMapping(value.GetType());

        public static RelationalTypeMapping GetMapping(
            [NotNull] this IRelationalTypeMapper typeMapper,
            [NotNull] IProperty property)
        {
            Check.NotNull(typeMapper, nameof(typeMapper));
            Check.NotNull(property, nameof(property));

            var mapping = typeMapper.FindMapping(property);
            if(mapping != null)
            {
                return mapping;
            }

            throw new NotSupportedException(RelationalStrings.UnsupportedType(property));
        }

        public static RelationalTypeMapping GetMapping(
            [NotNull] this IRelationalTypeMapper typeMapper,
            [NotNull] Type clrType)
        {
            Check.NotNull(typeMapper, nameof(typeMapper));
            Check.NotNull(clrType, nameof(clrType));

            var mapping = typeMapper.FindMapping(clrType);
            if (mapping != null)
            {
                return mapping;
            }

            throw new NotSupportedException(RelationalStrings.UnsupportedType(clrType));
        }

        public static RelationalTypeMapping GetMapping(
            [NotNull] this IRelationalTypeMapper typeMapper,
            [NotNull] string typeName)
        {
            Check.NotNull(typeMapper, nameof(typeMapper));
            Check.NotNull(typeName, nameof(typeName));

            var mapping = typeMapper.FindMapping(typeName);
            if (mapping != null)
            {
                return mapping;
            }

            throw new NotSupportedException(RelationalStrings.UnsupportedType(typeName));
        }
    }
}
