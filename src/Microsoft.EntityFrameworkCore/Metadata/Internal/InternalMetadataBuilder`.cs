// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;

namespace Microsoft.EntityFrameworkCore.Metadata.Internal
{
    public abstract class InternalMetadataBuilder<TMetadata> : InternalMetadataBuilder
        where TMetadata : ConventionalAnnotatable
    {
        protected InternalMetadataBuilder([NotNull] TMetadata metadata)
            : base(metadata)
        {
        }

        public new virtual TMetadata Metadata => (TMetadata)base.Metadata;
    }
}
