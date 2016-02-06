// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.EntityFrameworkCore.Scaffolding.Metadata.Internal
{
    public static class ScaffoldingAnnotationNames
    {
        public const string AnnotationPrefix = "Scaffolding:";
        public const string UseProviderMethodName = AnnotationPrefix + "UseProviderMethodName";
        public const string ColumnOrdinal = AnnotationPrefix + "ColumnOrdinal";
        public const string DependentEndNavigation = AnnotationPrefix + "DependentEndNavigation";
        public const string PrincipalEndNavigation = AnnotationPrefix + "PrincipalEndNavigation";
        public const string EntityTypeErrors = AnnotationPrefix + "EntityTypeErrors";
    }
}
