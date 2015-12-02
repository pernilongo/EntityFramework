// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.Data.Entity.Scaffolding.Internal;
using Xunit;

namespace Microsoft.Data.Entity.Relational.Design.ReverseEngineering
{
    public class ReverseEngineeringGeneratorTests
    {
        [Theory]
        [MemberData(nameof(NamespaceOptions))]
        public void Constructs_correct_namespace(
            string rootNamespace, string projectPath, string outputPath, string resultingNamespace)
        {
            Assert.Equal(resultingNamespace,
                ReverseEngineeringGenerator.ConstructNamespace(rootNamespace, projectPath, outputPath));
        }

        public static TheoryData NamespaceOptions
        {
            get
            {
                var data = new TheoryData<string, string, string, string>
                {
                    { "Root.Namespace", "project/Path", null, "Root.Namespace" },
                    { "Root.Namespace", "project/Path", "", "Root.Namespace" },
                    { "Root.Namespace", "project/Path", "/Absolute/Output/Path", "Root.Namespace" },
                    { "Root.Namespace", "project/Path", "../../Path/Outside/Project", "Root.Namespace" },
                    { "Root.Namespace", "project/Path", "Path/Inside/Project", "Root.Namespace.Path.Inside.Project" },
                    { "Root.Namespace", "project/Path", "Keyword/volatile/123/Bad!$&Chars", "Root.Namespace.Keyword._volatile._123.Bad___Chars" }
                };

                if (Path.DirectorySeparatorChar == '\\'
                    || Path.AltDirectorySeparatorChar == '\\')
                {
                    data.Add("Root.Namespace", @"project\Path", @"X:\Absolute\Output\Path", "Root.Namespace");
                    data.Add("Root.Namespace", @"project\Path", @"\Absolute\Output\Path", "Root.Namespace");
                    data.Add("Root.Namespace", @"project\Path", @"..\..\Path\Outside\Project", "Root.Namespace");
                    data.Add("Root.Namespace", @"project\Path", @"Path\Inside\Project", "Root.Namespace.Path.Inside.Project");
                    data.Add("Root.Namespace", @"project\Path", @"Keyword\volatile\123\Bad!$&Chars", "Root.Namespace.Keyword._volatile._123.Bad___Chars");
                }

                return data;
            }
        }
    }
}
