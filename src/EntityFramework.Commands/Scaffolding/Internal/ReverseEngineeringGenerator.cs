// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.Data.Entity.Scaffolding.Internal
{
    public class ReverseEngineeringGenerator
    {
        private readonly ConfigurationFactory _configurationFactory;
        private readonly IScaffoldingModelFactory _factory;

        public ReverseEngineeringGenerator(
            [NotNull] ILoggerFactory loggerFactory,
            [NotNull] IScaffoldingModelFactory scaffoldingModelFactory,
            [NotNull] ConfigurationFactory configurationFactory,
            [NotNull] CodeWriter codeWriter)
        {
            Check.NotNull(loggerFactory, nameof(loggerFactory));
            Check.NotNull(scaffoldingModelFactory, nameof(scaffoldingModelFactory));
            Check.NotNull(configurationFactory, nameof(configurationFactory));
            Check.NotNull(codeWriter, nameof(codeWriter));

            Logger = loggerFactory.CreateCommandsLogger();
            _factory = scaffoldingModelFactory;
            _configurationFactory = configurationFactory;
            CodeWriter = codeWriter;
        }

        public virtual ILogger Logger { get; }

        public virtual CodeWriter CodeWriter { get; }

        public virtual Task<ReverseEngineerFiles> GenerateAsync(
            [NotNull] ReverseEngineeringConfiguration configuration,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Check.NotNull(configuration, nameof(configuration));

            cancellationToken.ThrowIfCancellationRequested();

            configuration.CheckValidity();

            var metadataModel = GetMetadataModel(configuration);
            var @namespace = ConstructNamespace(configuration.ProjectRootNamespace,
                    configuration.ProjectPath, configuration.OutputPath);

            var customConfiguration = _configurationFactory
                .CreateCustomConfiguration(
                    configuration.ConnectionString, configuration.ContextClassName,
                    @namespace, configuration.UseFluentApiOnly);
            var modelConfiguration = _configurationFactory
                .CreateModelConfiguration(metadataModel, customConfiguration);

            var dbContextClassName =
                string.IsNullOrWhiteSpace(customConfiguration.ContextClassName)
                ? modelConfiguration.ClassName()
                : customConfiguration.ContextClassName;

            var outputPath = string.IsNullOrEmpty(configuration.OutputPath)
                ? configuration.ProjectPath
                : (Path.IsPathRooted(configuration.OutputPath)
                    ? configuration.OutputPath
                    : Path.Combine(configuration.ProjectPath, configuration.OutputPath));

            CheckOutputFiles(outputPath, dbContextClassName, metadataModel, configuration.OverwriteFiles);

            return CodeWriter.WriteCodeAsync(
                modelConfiguration, outputPath, dbContextClassName, cancellationToken);
        }

        public virtual IModel GetMetadataModel([NotNull] ReverseEngineeringConfiguration configuration)
        {
            Check.NotNull(configuration, nameof(configuration));

            var metadataModel = _factory.Create(
                configuration.ConnectionString, configuration.TableSelectionSet);
            if (metadataModel == null)
            {
                throw new InvalidOperationException(
                    RelationalDesignStrings.ProviderReturnedNullModel(
                        _factory.GetType().FullName));
            }

            return metadataModel;
        }

        public virtual void CheckOutputFiles(
            [NotNull] string outputPath,
            [NotNull] string dbContextClassName,
            [NotNull] IModel metadataModel,
            bool overwriteFiles)
        {
            Check.NotEmpty(outputPath, nameof(outputPath));
            Check.NotEmpty(dbContextClassName, nameof(dbContextClassName));
            Check.NotNull(metadataModel, nameof(metadataModel));

            var readOnlyFiles = CodeWriter.GetReadOnlyFilePaths(
                outputPath, dbContextClassName, metadataModel.GetEntityTypes());
            if (readOnlyFiles.Count > 0)
            {
                throw new InvalidOperationException(
                    RelationalDesignStrings.ReadOnlyFiles(
                        outputPath,
                        string.Join(
                            CultureInfo.CurrentCulture.TextInfo.ListSeparator, readOnlyFiles)));
            }

            if (!overwriteFiles)
            {
                var existingFiles = CodeWriter.GetExistingFilePaths(
                    outputPath, dbContextClassName, metadataModel.GetEntityTypes());
                if (existingFiles.Count > 0)
                {
                    throw new InvalidOperationException(
                        RelationalDesignStrings.ExistingFiles(
                            outputPath,
                            string.Join(
                                CultureInfo.CurrentCulture.TextInfo.ListSeparator, existingFiles)));
                }
            }
        }

        private static char[] directorySeparatorChars = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        public static string ConstructNamespace(
            [NotNull] string rootNamespace, [NotNull] string projectPath, [CanBeNull] string outputPath)
        {
            Check.NotEmpty(rootNamespace, nameof(rootNamespace));
            Check.NotEmpty(projectPath, nameof(projectPath));

            if (string.IsNullOrEmpty(outputPath)
                || Path.IsPathRooted(outputPath))
            {
                // outputPath is empty or is not relative - so just use root namespace
                return rootNamespace;
            }

            // strip off any directory separator chars at end of project path
            for (var projectPathLastChar = projectPath.Last();
                directorySeparatorChars.Contains(projectPathLastChar);
                projectPathLastChar = projectPath.Last())
            {
                projectPath = projectPath.Substring(0, projectPath.Length - 1);
            }

            var canonicalizedProjectPath = Path.GetFullPath(projectPath);
            var canonicalizedOutputPath = Path.GetFullPath(Path.Combine(projectPath, outputPath));
            if (!canonicalizedOutputPath.StartsWith(canonicalizedProjectPath))
            {
                // canonicalizedOutputPath is outside of project - so just use root namespace
                return rootNamespace;
            }

            var @namespace = rootNamespace;
            var canonicalizedRelativePath = canonicalizedOutputPath
                .Substring(canonicalizedProjectPath.Count() + 1);
            foreach (var pathPart in canonicalizedRelativePath
                .Split(directorySeparatorChars, StringSplitOptions.RemoveEmptyEntries))
            {
                @namespace += "." + CSharpUtilities.Instance.GenerateCSharpIdentifier(pathPart, null);
            }

            return @namespace;
        }
    }
}
