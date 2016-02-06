// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Internal;

namespace Microsoft.EntityFrameworkCore.Metadata.Internal
{
    public class ClrICollectionAccessor<TEntity, TCollection, TElement> : IClrCollectionAccessor
        where TEntity : class
        where TCollection : class, ICollection<TElement>
    {
        private readonly string _propertyName;
        private readonly Func<TEntity, TCollection> _getCollection;
        private readonly Action<TEntity, TCollection> _setCollection;
        private readonly Func<TEntity, Action<TEntity, TCollection>, TCollection> _createAndSetCollection;
        private readonly Func<TCollection> _createCollection;

        public virtual Type CollectionType => typeof(TCollection);

        public ClrICollectionAccessor(
            [NotNull] string propertyName,
            [NotNull] Func<TEntity, TCollection> getCollection,
            [CanBeNull] Action<TEntity, TCollection> setCollection,
            [CanBeNull] Func<TEntity, Action<TEntity, TCollection>, TCollection> createAndSetCollection,
            [CanBeNull] Func<TCollection> createCollection)
        {
            _propertyName = propertyName;
            _getCollection = getCollection;
            _setCollection = setCollection;
            _createAndSetCollection = createAndSetCollection;
            _createCollection = createCollection;
        }

        public virtual void Add(object instance, object value)
            => GetOrCreateCollection(instance).Add((TElement)value);

        public virtual void AddRange(object instance, IEnumerable<object> values)
        {
            var collection = GetOrCreateCollection(instance);

            foreach (TElement value in values)
            {
                if (!collection.Contains(value))
                {
                    collection.Add(value);
                }
            }
        }

        public virtual object Create(IEnumerable<object> values)
        {
            if (_createCollection == null)
            {
                throw new InvalidOperationException(CoreStrings.NavigationCannotCreateType(
                    _propertyName, typeof(TEntity).FullName, typeof(TCollection).FullName));
            }

            var collection = _createCollection();
            foreach (TElement value in values)
            {
                collection.Add(value);
            }

            return collection;
        }

        private TCollection GetOrCreateCollection(object instance)
        {
            var collection = _getCollection((TEntity)instance);

            if (collection == null)
            {
                if (_setCollection == null)
                {
                    throw new InvalidOperationException(CoreStrings.NavigationNoSetter(_propertyName, typeof(TEntity).FullName));
                }

                if (_createAndSetCollection == null)
                {
                    throw new InvalidOperationException(CoreStrings.NavigationCannotCreateType(
                        _propertyName, typeof(TEntity).FullName, typeof(TCollection).FullName));
                }

                collection = _createAndSetCollection((TEntity)instance, _setCollection);
            }
            return collection;
        }

        public virtual bool Contains(object instance, object value)
        {
            var collection = _getCollection((TEntity)instance);

            return (collection != null) && collection.Contains((TElement)value);
        }

        public virtual void Remove(object instance, object value)
            => _getCollection((TEntity)instance)?.Remove((TElement)value);
    }
}
