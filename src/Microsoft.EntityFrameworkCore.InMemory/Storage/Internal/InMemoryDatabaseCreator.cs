// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.Storage.Internal
{
    public class InMemoryDatabaseCreator : IDatabaseCreator
    {
        private readonly IModel _model;
        private readonly IInMemoryDatabase _database;

        public InMemoryDatabaseCreator([NotNull] IInMemoryDatabase database, [NotNull] IModel model)
        {
            Check.NotNull(database, nameof(database));
            Check.NotNull(model, nameof(model));

            _database = database;
            _model = model;
        }

        public virtual bool EnsureDeleted() 
            => _database.Store.Clear();

        public virtual Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default(CancellationToken))
            => Task.FromResult(EnsureDeleted());

        public virtual bool EnsureCreated() => _database.EnsureDatabaseCreated(_model);

        public virtual Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default(CancellationToken))
            => Task.FromResult(_database.EnsureDatabaseCreated(_model));
    }
}
