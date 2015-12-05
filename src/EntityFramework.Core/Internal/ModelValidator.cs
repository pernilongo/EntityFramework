// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Metadata.Internal;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Internal
{
    public abstract class ModelValidator : IModelValidator
    {
        public virtual void Validate(IModel model)
        {
            EnsureNoShadowEntities(model);
            EnsureNoShadowKeys(model);
            EnsureNonNullPrimaryKeys(model);
            EnsureClrInheritance(model);
        }

        protected virtual void EnsureNoShadowEntities([NotNull] IModel model)
        {
            var firstShadowEntity = model.GetEntityTypes().FirstOrDefault(entityType => !entityType.HasClrType());
            if (firstShadowEntity != null)
            {
                ShowError(CoreStrings.ShadowEntity(firstShadowEntity.Name));
            }
        }

        protected virtual void EnsureNoShadowKeys([NotNull] IModel model)
        {
            foreach (var entityType in model.GetEntityTypes())
            {
                foreach (var key in entityType.GetKeys())
                {
                    if (key.Properties.Any(p => p.IsShadowProperty))
                    {
                        string message;
                        var referencingFk = key.FindReferencingForeignKeys().FirstOrDefault();
                        if (referencingFk != null)
                        {
                            message = CoreStrings.ReferencedShadowKey(
                                Property.Format(key.Properties),
                                entityType.Name,
                                Property.Format(key.Properties.Where(p => p.IsShadowProperty)),
                                Property.Format(referencingFk.Properties),
                                referencingFk.DeclaringEntityType.Name);
                        }
                        else
                        {
                            message = CoreStrings.ShadowKey(
                                Property.Format(key.Properties),
                                entityType.Name,
                                Property.Format(key.Properties.Where(p => p.IsShadowProperty)));
                        }

                        ShowWarning(message);
                    }
                }
            }
        }

        protected virtual void EnsureNonNullPrimaryKeys([NotNull] IModel model)
        {
            Check.NotNull(model, nameof(model));

            var entityTypeWithNullPk = model.GetEntityTypes().FirstOrDefault(et => et.FindPrimaryKey() == null);
            if (entityTypeWithNullPk != null)
            {
                ShowError(CoreStrings.EntityRequiresKey(entityTypeWithNullPk.Name));
            }
        }

        protected virtual void EnsureClrInheritance([NotNull] IModel model)
        {
            var validEntityTypes = new HashSet<IEntityType>();
            foreach (var entityType in model.GetEntityTypes())
            {
                EnsureClrInheritance(model, entityType, validEntityTypes);
            }
        }

        private void EnsureClrInheritance(IModel model, IEntityType entityType, HashSet<IEntityType> validEntityTypes)
        {
            if (validEntityTypes.Contains(entityType))
            {
                return;
            }

            var baseClrType = entityType.ClrType?.GetTypeInfo().BaseType;
            while (baseClrType != null)
            {
                var baseEntityType = model.FindEntityType(baseClrType);
                if (baseEntityType != null)
                {
                    if (!baseEntityType.IsAssignableFrom(entityType))
                    {
                        ShowError(CoreStrings.InconsistentInheritance(entityType.DisplayName(), baseEntityType.DisplayName()));
                    }
                    EnsureClrInheritance(model, baseEntityType, validEntityTypes);
                    break;
                }
                baseClrType = baseClrType.GetTypeInfo().BaseType;
            }
            validEntityTypes.Add(entityType);
        }

        protected virtual void ShowError([NotNull] string message)
        {
            throw new InvalidOperationException(message);
        }

        protected abstract void ShowWarning([NotNull] string message);
    }
}
