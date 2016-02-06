// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Internal;

namespace Microsoft.EntityFrameworkCore.Metadata.Internal
{
    public class ClrCollectionAccessorFactory
    {
        private static readonly MethodInfo _genericCreate
            = typeof(ClrCollectionAccessorFactory).GetTypeInfo().GetDeclaredMethod(nameof(CreateGeneric));

        private static readonly MethodInfo _createAndSet
            = typeof(ClrCollectionAccessorFactory).GetTypeInfo().GetDeclaredMethod(nameof(CreateAndSet));

        private static readonly MethodInfo _create
            = typeof(ClrCollectionAccessorFactory).GetTypeInfo().GetDeclaredMethod(nameof(CreateCollection));

        [CallsMakeGenericMethod(MethodName = nameof(CreateGeneric), TypeArguments = new [] { typeof(object), typeof(TypeArgumentCategory.EntityTypeCollections), typeof(TypeArgumentCategory.EntityTypes) })]
        public virtual IClrCollectionAccessor Create([NotNull] INavigation navigation)
        {
            var accessor = navigation as IClrCollectionAccessor;

            if (accessor != null)
            {
                return accessor;
            }

            var property = navigation.DeclaringEntityType.ClrType.GetAnyProperty(navigation.Name);
            var elementType = property.PropertyType.TryGetElementType(typeof(ICollection<>));

            // TODO: Only ICollections supported; add support for enumerables with add/remove methods
            // Issue #752
            if (elementType == null)
            {
                throw new NotSupportedException(
                    CoreStrings.NavigationBadType(
                        navigation.Name, navigation.DeclaringEntityType.Name, property.PropertyType.FullName, navigation.GetTargetType().Name));
            }

            if (property.PropertyType.IsArray)
            {
                throw new NotSupportedException(
                    CoreStrings.NavigationArray(navigation.Name, navigation.DeclaringEntityType.Name, property.PropertyType.FullName));
            }

            if (property.GetMethod == null)
            {
                throw new NotSupportedException(CoreStrings.NavigationNoGetter(navigation.Name, navigation.DeclaringEntityType.Name));
            }

            var boundMethod = _genericCreate.MakeGenericMethod(
                property.DeclaringType, property.PropertyType, elementType);

            return (IClrCollectionAccessor)boundMethod.Invoke(null, new object[] { property });
        }

        [CallsMakeGenericMethod(MethodName = nameof(CreateAndSet), TypeArguments = new[] { typeof(object), typeof(object), typeof(object)})]
        [CallsMakeGenericMethod(MethodName = nameof(CreateCollection), TypeArguments = new[] { typeof(object), typeof(object) })]
        // ReSharper disable once UnusedMember.Local
        private static IClrCollectionAccessor CreateGeneric<TEntity, TCollection, TElement>(PropertyInfo property)
            where TEntity : class
            where TCollection : class, ICollection<TElement>
        {
            var getterDelegate = (Func<TEntity, TCollection>)property.GetMethod.CreateDelegate(typeof(Func<TEntity, TCollection>));

            Action<TEntity, TCollection> setterDelegate = null;
            Func<TEntity, Action<TEntity, TCollection>, TCollection> createAndSetDelegate = null;
            Func<TCollection> createDelegate = null;

            var setter = property.SetMethod;
            if (setter != null)
            {
                setterDelegate = (Action<TEntity, TCollection>)setter.CreateDelegate(typeof(Action<TEntity, TCollection>));

                var concreteType = new CollectionTypeFactory().TryFindTypeToInstantiate(typeof(TCollection));

                if (concreteType != null)
                {
                    createAndSetDelegate = (Func<TEntity, Action<TEntity, TCollection>, TCollection>)_createAndSet
                        .MakeGenericMethod(typeof(TEntity), typeof(TCollection), concreteType)
                        .CreateDelegate(typeof(Func<TEntity, Action<TEntity, TCollection>, TCollection>));

                    createDelegate = (Func<TCollection>)_create
                        .MakeGenericMethod(typeof(TCollection), concreteType)
                        .CreateDelegate(typeof(Func<TCollection>));
                }
            }

            return new ClrICollectionAccessor<TEntity, TCollection, TElement>(
                property.Name, getterDelegate, setterDelegate, createAndSetDelegate, createDelegate);
        }

        // ReSharper disable once UnusedMember.Local
        private static TCollection CreateAndSet<TEntity, TCollection, TConcreteCollection>(
            TEntity entity,
            Action<TEntity, TCollection> setterDelegate)
            where TEntity : class
            where TCollection : class
            where TConcreteCollection : TCollection, new()
        {
            var collection = new TConcreteCollection();
            setterDelegate(entity, collection);
            return collection;
        }

        // ReSharper disable once UnusedMember.Local
        private static TCollection CreateCollection<TCollection, TConcreteCollection>()
            where TCollection : class
            where TConcreteCollection : TCollection, new()
        {
            return new TConcreteCollection();
        }
    }
}
