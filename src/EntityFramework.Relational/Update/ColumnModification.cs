// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Update
{
    public class ColumnModification
    {
        private readonly LazyRef<string> _parameterName;
        private readonly LazyRef<string> _originalParameterName;
        private readonly LazyRef<string> _outputParameterName;

        public ColumnModification(
            [NotNull] IUpdateEntry entry,
            [NotNull] IProperty property,
            [NotNull] IRelationalPropertyAnnotations propertyAnnotations,
            [NotNull] ParameterNameGenerator parameterNameGenerator,
            bool isRead,
            bool isWrite,
            bool isKey,
            bool isCondition)
        {
            Check.NotNull(entry, nameof(entry));
            Check.NotNull(property, nameof(property));
            Check.NotNull(propertyAnnotations, nameof(propertyAnnotations));
            Check.NotNull(parameterNameGenerator, nameof(parameterNameGenerator));

            Entry = entry;
            Property = property;
            ColumnName = propertyAnnotations.ColumnName;

            _parameterName = isWrite
                ? new LazyRef<string>(parameterNameGenerator.GenerateNext)
                : new LazyRef<string>((string)null);
            _originalParameterName = isCondition
                ? new LazyRef<string>(parameterNameGenerator.GenerateNext)
                : new LazyRef<string>((string)null);
            _outputParameterName = isRead
                ? new LazyRef<string>(parameterNameGenerator.GenerateNext)
                : new LazyRef<string>((string)null);

            IsRead = isRead;
            IsWrite = isWrite;
            IsKey = isKey;
            IsCondition = isCondition;
        }

        public virtual IUpdateEntry Entry { get; }

        public virtual IProperty Property { get; }

        public virtual bool IsRead { get; }

        public virtual bool IsWrite { get; }

        public virtual bool IsCondition { get; }

        public virtual bool IsKey { get; }

        public virtual string ParameterName
            => _parameterName.Value;

        public virtual string OriginalParameterName
            => _originalParameterName.Value;

        public virtual string OutputParameterName
            => _outputParameterName.Value;

        public virtual string ColumnName { get; }

        public virtual object OriginalValue => Entry.GetOriginalValue(Property);

        public virtual object Value
        {
            get { return Entry.GetCurrentValue(Property); }
            [param: CanBeNull] set { Entry.SetCurrentValue(Property, value); }
        }
    }
}
