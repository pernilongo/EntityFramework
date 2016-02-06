// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if DNX451

using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore.Commands.TestUtilities;
using Microsoft.EntityFrameworkCore.FunctionalTests.TestUtilities.Xunit;
using Microsoft.EntityFrameworkCore.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.EntityFrameworkCore.Design.Internal
{
    [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Mono's assembly loading mechanisms are buggy")]
    public class OperationExecutorTest
    {
        private readonly ITestOutputHelper _output = null;

        public OperationExecutorTest(ITestOutputHelper output)
        {
            // uncomment line below to see output when developing
            _output = output;
        }

        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Mono's assembly loading mechanisms are buggy")]
        public class SimpleProjectTest : IClassFixture<SimpleProjectTest.SimpleProject>
        {
            private readonly SimpleProject _project;

            public SimpleProjectTest(SimpleProject project)
            {
                _project = project;
            }

            [ConditionalFact]
            public void GetContextType_works_cross_domain()
            {
                var contextTypeName = _project.Executor.GetContextType("SimpleContext");
                Assert.StartsWith("SimpleProject.SimpleContext, ", contextTypeName);
            }

            [ConditionalFact]
            public void AddMigration_works_cross_domain()
            {
                var artifacts = _project.Executor.AddMigration("EmptyMigration", "Migrationz", "SimpleContext");
                Assert.Equal(3, artifacts.Count());
                Assert.True(Directory.Exists(Path.Combine(_project.TargetDir, @"Migrationz")));
            }

            [ConditionalFact]
            public void ScriptMigration_works_cross_domain()
            {
                var sql = _project.Executor.ScriptMigration(null, "InitialCreate", false, "SimpleContext");
                Assert.NotEmpty(sql);
            }

            [ConditionalFact]
            public void GetContextTypes_works_cross_domain()
            {
                var contextTypes = _project.Executor.GetContextTypes();
                Assert.Equal(1, contextTypes.Count());
            }

            [ConditionalFact]
            public void GetMigrations_works_cross_domain()
            {
                var migrations = _project.Executor.GetMigrations("SimpleContext");
                Assert.Equal(1, migrations.Count());
            }

            public class SimpleProject : IDisposable
            {
                private readonly TempDirectory _directory = new TempDirectory();

                public SimpleProject()
                {
                    var source = new BuildSource
                    {
                        TargetDir = TargetDir,
                        References =
                                {
                                    BuildReference.ByName("System.Diagnostics.DiagnosticSource", copyLocal: true),
                                    BuildReference.ByName("System.Interactive.Async", copyLocal: true),
                                    BuildReference.ByName("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore", copyLocal: true),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore.Commands", copyLocal: true),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational", copyLocal: true),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational.Design", copyLocal: true),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore.SqlServer", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Caching.Abstractions", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Caching.Memory", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.DependencyInjection", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.DependencyInjection.Abstractions", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Logging", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Logging.Abstractions", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Options", copyLocal: true),
                                    BuildReference.ByName("Remotion.Linq", copyLocal: true)
                                },
                        Sources = { @"
                            using Microsoft.EntityFrameworkCore;
                            using Microsoft.EntityFrameworkCore.Infrastructure;
                            using Microsoft.EntityFrameworkCore.Migrations;

                            namespace SimpleProject
                            {
                                internal class SimpleContext : DbContext
                                {
                                    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                                    {
                                        optionsBuilder.UseSqlServer(""Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=SimpleProject.SimpleContext;Integrated Security=True"");
                                    }
                                }

                                namespace Migrations
                                {
                                    [DbContext(typeof(SimpleContext))]
                                    [Migration(""20141010222726_InitialCreate"")]
                                    public class InitialCreate : Migration
                                    {
                                        protected override void Up(MigrationBuilder migrationBuilder)
                                        {
                                        }
                                    }
                                }
                            }" }
                    };
                    var build = source.Build();
                    Executor = new OperationExecutorWrapper(TargetDir, build.TargetName, TargetDir, "SimpleProject");
                }

                public string TargetDir
                {
                    get { return _directory.Path; }
                }

                public OperationExecutorWrapper Executor { get; }

                public void Dispose()
                {
                    Executor.Dispose();
                    _directory.Dispose();
                }
            }
        }

        [ConditionalFact]
        public void GetMigrations_filters_by_context_name()
        {
            using (var directory = new TempDirectory())
            {
                var targetDir = directory.Path;
                var source = new BuildSource
                {
                    TargetDir = targetDir,
                    References =
                            {
                                BuildReference.ByName("System.Diagnostics.DiagnosticSource", copyLocal: true),
                                BuildReference.ByName("System.Interactive.Async", copyLocal: true),
                                BuildReference.ByName("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore", copyLocal: true),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore.Commands", copyLocal: true),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational", copyLocal: true),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational.Design", copyLocal: true),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore.SqlServer", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.Caching.Abstractions", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.Caching.Memory", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.DependencyInjection", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.DependencyInjection.Abstractions", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.Logging", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.Logging.Abstractions", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.Options", copyLocal: true),
                                BuildReference.ByName("Remotion.Linq", copyLocal: true)
                            },
                    Sources = { @"
                        using Microsoft.EntityFrameworkCore;
                        using Microsoft.EntityFrameworkCore.Infrastructure;
                        using Microsoft.EntityFrameworkCore.Migrations;

                        namespace MyProject
                        {
                            internal class Context1 : DbContext
                            {
                                protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                                {
                                    optionsBuilder.UseSqlServer(""Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=SimpleProject.SimpleContext;Integrated Security=True"");
                                }
                            }

                            internal class Context2 : DbContext
                            {
                            }

                            namespace Migrations
                            {
                                namespace Context1Migrations
                                {
                                    [DbContext(typeof(Context1))]
                                    [Migration(""000000000000000_Context1Migration"")]
                                    public class Context1Migration : Migration
                                    {
                                        protected override void Up(MigrationBuilder migrationBuilder)
                                        {
                                        }
                                    }
                                }

                                namespace Context2Migrations
                                {
                                    [DbContext(typeof(Context2))]
                                    [Migration(""000000000000000_Context2Migration"")]
                                    public class Context2Migration : Migration
                                    {
                                        protected override void Up(MigrationBuilder migrationBuilder)
                                        {
                                        }
                                    }
                                }
                            }
                        }" }
                };
                var build = source.Build();
                using (var executor = new OperationExecutorWrapper(targetDir, build.TargetName, targetDir, "MyProject"))
                {
                    var migrations = executor.GetMigrations("Context1");

                    Assert.Equal(1, migrations.Count());
                }
            }
        }

        [ConditionalFact]
        public void GetContextType_works_with_multiple_assemblies()
        {
            using (var directory = new TempDirectory())
            {
                var targetDir = directory.Path;
                var contextsSource = new BuildSource
                {
                    TargetDir = targetDir,
                    References =
                            {
                                BuildReference.ByName("Microsoft.EntityFrameworkCore", copyLocal: true),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore.Commands", copyLocal: true)
                            },
                    Sources = { @"
                        using Microsoft.EntityFrameworkCore;

                        namespace MyProject
                        {
                            public class Context1 : DbContext
                            {
                            }

                            public class Context2 : DbContext
                            {
                            }
                        }" }
                };
                var contextsBuild = contextsSource.Build();
                var migrationsSource = new BuildSource
                {
                    TargetDir = targetDir,
                    References =
                            {
                                BuildReference.ByName("System.Reflection.Metadata", copyLocal: true),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore"),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational", copyLocal: true),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational.Design", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.DependencyInjection", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.DependencyInjection.Abstractions", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.Logging", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.Logging.Abstractions", copyLocal: true),
                                BuildReference.ByPath(contextsBuild.TargetPath)
                            },
                    Sources = { @"
                        using Microsoft.EntityFrameworkCore;
                        using Microsoft.EntityFrameworkCore.Infrastructure;
                        using Microsoft.EntityFrameworkCore.Migrations;

                        namespace MyProject
                        {
                            internal class Context3 : DbContext
                            {
                            }

                            namespace Migrations
                            {
                                namespace Context1Migrations
                                {
                                    [DbContext(typeof(Context1))]
                                    [Migration(""000000000000000_Context1Migration"")]
                                    public class Context1Migration : Migration
                                    {
                                        protected override void Up(MigrationBuilder migrationBuilder)
                                        {
                                        }
                                    }
                                }

                                namespace Context2Migrations
                                {
                                    [DbContext(typeof(Context2))]
                                    [Migration(""000000000000000_Context2Migration"")]
                                    public class Context2Migration : Migration
                                    {
                                        protected override void Up(MigrationBuilder migrationBuilder)
                                        {
                                        }
                                    }
                                }
                            }
                        }" }
                };
                var migrationsBuild = migrationsSource.Build();
                using (var executor = new OperationExecutorWrapper(targetDir, migrationsBuild.TargetName, targetDir, "MyProject"))
                {
                    var contextTypes = executor.GetContextTypes();

                    Assert.Equal(3, contextTypes.Count());
                }
            }
        }

        [ConditionalFact]
        public void AddMigration_begins_new_namespace_when_foreign_migrations()
        {
            using (var directory = new TempDirectory())
            {
                var targetDir = directory.Path;
                var source = new BuildSource
                {
                    TargetDir = targetDir,
                    References =
                                {
                                    BuildReference.ByName("System.Diagnostics.DiagnosticSource", copyLocal: true),
                                    BuildReference.ByName("System.Interactive.Async", copyLocal: true),
                                    BuildReference.ByName("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore", copyLocal: true),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore.Commands", copyLocal: true),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational", copyLocal: true),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational.Design", copyLocal: true),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore.SqlServer", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Caching.Abstractions", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Caching.Memory", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.DependencyInjection", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.DependencyInjection.Abstractions", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Logging", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Logging.Abstractions", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Options", copyLocal: true),
                                    BuildReference.ByName("Remotion.Linq", copyLocal: true)
                                },
                    Sources = { @"
                            using Microsoft.EntityFrameworkCore;
                            using Microsoft.EntityFrameworkCore.Infrastructure;
                            using Microsoft.EntityFrameworkCore.Migrations;

                            namespace MyProject
                            {
                                internal class MyFirstContext : DbContext
                                {
                                    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                                    {
                                        optionsBuilder.UseSqlServer(""Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=MyProject.MyFirstContext"");
                                    }
                                }

                                internal class MySecondContext : DbContext
                                {
                                    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                                    {
                                        optionsBuilder.UseSqlServer(""Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=MyProject.MySecondContext"");
                                    }
                                }

                                namespace Migrations
                                {
                                    [DbContext(typeof(MyFirstContext))]
                                    [Migration(""20151006140723_InitialCreate"")]
                                    public class InitialCreate : Migration
                                    {
                                        protected override void Up(MigrationBuilder migrationBuilder)
                                        {
                                        }
                                    }
                                }
                            }" }
                };
                var build = source.Build();
                using (var executor = new OperationExecutorWrapper(targetDir, build.TargetName, targetDir, "MyProject"))
                {
                    var artifacts = executor.AddMigration("MyMigration", /*outputDir:*/ null, "MySecondContext");
                    Assert.Equal(3, artifacts.Count());
                    Assert.True(Directory.Exists(Path.Combine(targetDir, @"Migrations\MySecond")));
                }
            }
        }

        [ConditionalFact]
        public void ScaffoldRuntimeDirectives()
        {
            using (var directory = new TempDirectory())
            {
                var targetDir = directory.Path;
                var source = new BuildSource
                {
                    TargetDir = targetDir,
                    References =
                                {
                                    BuildReference.ByName("System.Diagnostics.DiagnosticSource", copyLocal: true),
                                    BuildReference.ByName("System.Interactive.Async", copyLocal: true),
                                    BuildReference.ByName("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore", copyLocal: true),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore.Commands", copyLocal: true),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational", copyLocal: true),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational.Design", copyLocal: true),
                                    BuildReference.ByName("Microsoft.EntityFrameworkCore.SqlServer", copyLocal: true),
                                    BuildReference.ByName("Microsoft.CodeAnalysis", copyLocal: true),
                                    BuildReference.ByName("Microsoft.CodeAnalysis.CSharp", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Caching.Abstractions", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Caching.Memory", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.DependencyInjection", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.DependencyInjection.Abstractions", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Logging", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Logging.Abstractions", copyLocal: true),
                                    BuildReference.ByName("Microsoft.Extensions.Options", copyLocal: true),
                                    BuildReference.ByName("Remotion.Linq", copyLocal: true)
                                },
                    Sources = { @"using System;
                                using Microsoft.EntityFrameworkCore;

                                namespace MyProject
                                {
                                    public class MyContext : DbContext
                                    {
                                        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                                        {
                                            optionsBuilder.UseSqlServer(""Data Source=(localdb)\\mssqllocaldb"");
                                        }
                                        public DbSet<Post> Posts { get; set; }
                                    }

                                    public class MySecondContext : DbContext
                                    {
                                        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                                        {
                                            optionsBuilder.UseSqlServer(""Data Source=(localdb)\\mssqllocaldb"");
                                        }

                                        public DbSet<Blog> Blogs { get; set; }
                                    }

                                    public class Blog
                                    {
                                        public int Id { get; set; }
                                        public DateTime Created { get; set; }
                                        public string Title { get; set; }
                                    }

                                    public class Post
                                    {
                                        public int Id { get; set; }
                                        public string Title { get; set; }
                                    }
                                }" }
                };
                var build = source.Build();
                using (var executor = new OperationExecutorWrapper(targetDir, build.TargetName, targetDir, "MyProject"))
                {
                    executor.ScaffoldRuntimeDirectives();
                    var expectedFile = Path.Combine(targetDir, @"Properties\Microsoft.EntityFrameworkCore.g.rd.xml");

                    Assert.True(File.Exists(expectedFile));

                    var contents = File.ReadAllText(expectedFile);
                    _output?.WriteLine(contents);

                    // original values snapshot
                    Assert.Contains("<TypeInstantiation Name=\"Microsoft.EntityFrameworkCore.ChangeTracking.Internal.Snapshot\" "
                        + "Arguments=\"System.Int32, System.DateTime, System.String\" "
                        + "Dynamic=\"Required All\" />", 
                        contents);
                }
            }
        }

        [ConditionalFact]
        public void GetMigrations_throws_when_target_and_migrations_assemblies_mismatch()
        {
            using (var directory = new TempDirectory())
            {
                var targetDir = directory.Path;
                var source = new BuildSource
                {
                    TargetDir = targetDir,
                    References =
                            {
                                BuildReference.ByName("System.Diagnostics.DiagnosticSource", copyLocal: true),
                                BuildReference.ByName("System.Interactive.Async", copyLocal: true),
                                BuildReference.ByName("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore", copyLocal: true),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore.Commands", copyLocal: true),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational", copyLocal: true),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational.Design", copyLocal: true),
                                BuildReference.ByName("Microsoft.EntityFrameworkCore.SqlServer", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.Caching.Abstractions", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.Caching.Memory", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.DependencyInjection", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.DependencyInjection.Abstractions", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.Logging", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.Logging.Abstractions", copyLocal: true),
                                BuildReference.ByName("Microsoft.Extensions.Options", copyLocal: true),
                                BuildReference.ByName("Remotion.Linq", copyLocal: true)
                            },
                    Sources = { @"
                            using Microsoft.EntityFrameworkCore;
                            using Microsoft.EntityFrameworkCore.Infrastructure;
                            using Microsoft.EntityFrameworkCore.Migrations;

                            namespace MyProject
                            {
                                internal class MyContext : DbContext
                                {
                                    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                                    {
                                        optionsBuilder
                                            .UseSqlServer(""Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=MyProject.MyContext;Integrated Security=True"")
                                            .MigrationsAssembly(""UnknownAssembly"");
                                    }
                                }

                                namespace Migrations
                                {
                                    [DbContext(typeof(MyContext))]
                                    [Migration(""20151215152142_MyMigration"")]
                                    public class MyMigration : Migration
                                    {
                                        protected override void Up(MigrationBuilder migrationBuilder)
                                        {
                                        }
                                    }
                                }
                            }" }
                };
                var build = source.Build();
                using (var executor = new OperationExecutorWrapper(targetDir, build.TargetName, targetDir, "MyProject"))
                {
                    var ex = Assert.Throws<WrappedOperationException>(
                        () => executor.GetMigrations("MyContext"));

                    Assert.Equal(
                        CommandsStrings.MigrationsAssemblyMismatch(build.TargetName, "UnknownAssembly"),
                        ex.Message);
                }
            }
        }
    }
}

#endif
