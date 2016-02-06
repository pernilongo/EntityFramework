// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Clauses.ResultOperators;
using Remotion.Linq.Clauses.StreamedData;

namespace Microsoft.EntityFrameworkCore.Query.ResultOperators.Internal
{
    public class TrackingResultOperator : SequenceTypePreservingResultOperatorBase, IQueryAnnotation
    {
        public TrackingResultOperator(bool tracking)
        {
            IsTracking = tracking;
        }

        public virtual IQuerySource QuerySource { get; [NotNull] set; }
        public virtual QueryModel QueryModel { get; [NotNull] set; }

        public virtual bool IsTracking { get; }

        public override string ToString() => IsTracking ? "AsTracking()" : "AsNoTracking()";

        public override ResultOperatorBase Clone([NotNull] CloneContext cloneContext)
            => new TrackingResultOperator(IsTracking);

        public override void TransformExpressions([NotNull] Func<Expression, Expression> transformation)
        {
        }

        public override StreamedSequence ExecuteInMemory<T>([NotNull] StreamedSequence input) => input;
    }
}
