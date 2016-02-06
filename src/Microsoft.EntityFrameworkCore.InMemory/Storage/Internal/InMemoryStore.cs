// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Extensions.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Storage.Internal
{
    public class InMemoryStore : IInMemoryStore
    {
        private readonly IInMemoryTableFactory _tableFactory;
        private readonly ILogger _logger;

        private readonly object _lock = new object();

        private Lazy<Dictionary<IEntityType, IInMemoryTable>> _tables = CreateTables();

        public InMemoryStore(
            [NotNull] IInMemoryTableFactory tableFactory,
            [NotNull] ILogger<InMemoryStore> logger)
        {
            _tableFactory = tableFactory;
            _logger = logger;
        }

        /// <summary>
        ///     Returns true just after the database has been created, false thereafter
        /// </summary>
        /// <returns>
        ///     true if the database has just been created, false otherwise
        /// </returns>
        public virtual bool EnsureCreated(IModel model)
        {
            lock (_lock)
            {
                var returnValue = !_tables.IsValueCreated;

                // ReSharper disable once UnusedVariable
                var _ = _tables.Value;

                return returnValue;
            }
        }

        public virtual bool Clear()
        {
            lock (_lock)
            {
                if (!_tables.IsValueCreated)
                {
                    return false;
                }

                _tables = CreateTables();
                return true;
            }
        }

        private static Lazy<Dictionary<IEntityType, IInMemoryTable>> CreateTables()
        {
            return new Lazy<Dictionary<IEntityType, IInMemoryTable>>(
                () => new Dictionary<IEntityType, IInMemoryTable>(new EntityTypeNameEqualityComparer()),
                LazyThreadSafetyMode.PublicationOnly);
        }

        public virtual IReadOnlyList<InMemoryTableSnapshot> GetTables(IEntityType entityType)
        {
            var data = new List<InMemoryTableSnapshot>();
            lock (_lock)
            {
                if (_tables.IsValueCreated)
                {
                    foreach (var et in entityType.GetConcreteTypesInHierarchy())
                    {
                        IInMemoryTable table;

                        if (_tables.Value.TryGetValue(et, out table))
                        {
                            data.Add(new InMemoryTableSnapshot(et, table.SnapshotRows()));
                        }
                    }
                }
            }
            return data;
        }

        public virtual int ExecuteTransaction(IEnumerable<IUpdateEntry> entries)
        {
            var rowsAffected = 0;

            lock (_lock)
            {
                foreach (var entry in entries)
                {
                    var entityType = entry.EntityType;

                    Debug.Assert(!entityType.IsAbstract());

                    IInMemoryTable table;
                    if (!_tables.Value.TryGetValue(entityType, out table))
                    {
                        _tables.Value.Add(entityType, table = _tableFactory.Create(entityType));
                    }

                    switch (entry.EntityState)
                    {
                        case EntityState.Added:
                            table.Create(entry);
                            break;
                        case EntityState.Deleted:
                            table.Delete(entry);
                            break;
                        case EntityState.Modified:
                            table.Update(entry);
                            break;
                    }

                    rowsAffected++;
                }
            }

            _logger.LogInformation<object>(
                InMemoryLoggingEventId.SavedChanges,
                rowsAffected,
                InMemoryStrings.LogSavedChanges);

            return rowsAffected;
        }

    }
}
