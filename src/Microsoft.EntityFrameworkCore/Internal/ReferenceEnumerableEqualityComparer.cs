// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.EntityFrameworkCore.Internal
{
    public sealed class ReferenceEnumerableEqualityComparer<TEnumerable, TValue> : IEqualityComparer<TEnumerable>
        where TEnumerable : IEnumerable<TValue>
    {
        public bool Equals(TEnumerable x, TEnumerable y) => x.SequenceEqual(y);

        public int GetHashCode(TEnumerable obj) => obj.Aggregate(0, (t, v) => (t * 397) ^ v.GetHashCode());
    }
}
