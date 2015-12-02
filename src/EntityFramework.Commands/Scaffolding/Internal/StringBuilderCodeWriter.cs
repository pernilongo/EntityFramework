// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata.Internal;
using Microsoft.Data.Entity.Scaffolding.Internal.Configuration;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Data.Entity.Metadata;
using System.IO;

namespace Microsoft.Data.Entity.Scaffolding.Internal
{
    public class StringBuilderCodeWriter : CodeWriter
    {
        public virtual DbContextWriter DbContextWriter { get; }
        public virtual EntityTypeWriter EntityTypeWriter { get; }

        public virtual IRelationalAnnotationProvider ExtensionsProvider { get; private set; }

        public StringBuilderCodeWriter(
            [NotNull] IFileService fileService,
            [NotNull] DbContextWriter dbContextWriter,
            [NotNull] EntityTypeWriter entityTypeWriter,
            [NotNull] IRelationalAnnotationProvider extensionsProvider)
            : base(fileService)
        {
            Check.NotNull(dbContextWriter, nameof(dbContextWriter));
            Check.NotNull(entityTypeWriter, nameof(entityTypeWriter));
            Check.NotNull(extensionsProvider, nameof(extensionsProvider));

            DbContextWriter = dbContextWriter;
            EntityTypeWriter = entityTypeWriter;
            ExtensionsProvider = extensionsProvider;
        }

        public override Task<ReverseEngineerFiles> WriteCodeAsync(
            [NotNull] ModelConfiguration modelConfiguration,
            [NotNull] string outputPath,
            [NotNull] string dbContextClassName,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Check.NotNull(modelConfiguration, nameof(modelConfiguration));
            Check.NotEmpty(outputPath, nameof(outputPath));
            Check.NotEmpty(dbContextClassName, nameof(dbContextClassName));

            cancellationToken.ThrowIfCancellationRequested();

            var resultingFiles = new ReverseEngineerFiles();

            var generatedCode = DbContextWriter.WriteCode(modelConfiguration);

            // output DbContext .cs file
            var dbContextFileName = dbContextClassName + FileExtension;
            var dbContextFileFullPath = FileService.OutputFile(
                outputPath, dbContextFileName, generatedCode);
            resultingFiles.ContextFile = dbContextFileFullPath;

            foreach (var entityConfig in modelConfiguration.EntityConfigurations)
            {
                generatedCode = EntityTypeWriter.WriteCode(entityConfig);

                // output EntityType poco .cs file
                var schema = ExtensionsProvider.For(entityConfig.EntityType).Schema ?? string.Empty;
                var entityTypeFileName = entityConfig.EntityType.DisplayName() + FileExtension;
                var entityTypeFileFullPath = FileService.OutputFile(
                    Path.Combine(outputPath, schema), entityTypeFileName, generatedCode);
                resultingFiles.EntityTypeFiles.Add(entityTypeFileFullPath);
            }

            return Task.FromResult(resultingFiles);
        }
    }
}
