// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Common;
using JetBrains.Annotations;

namespace Microsoft.EntityFrameworkCore.Storage
{
    public interface IRelationalParameter
    {
        string InvariantName { get; }

        void AddDbParameter([NotNull] DbCommand command, [CanBeNull] object value);
    }
}
