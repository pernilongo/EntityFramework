// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.FunctionalTests;
using Microsoft.EntityFrameworkCore.FunctionalTests.TestUtilities.Xunit;
using Microsoft.EntityFrameworkCore.SqlServer.FunctionalTests.SqlAzure.Model;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.EntityFrameworkCore.SqlServer.FunctionalTests.SqlAzure
{
    [SqlServerCondition(SqlServerCondition.IsSqlAzure)]
    public class SqlAzureBatchingTest : IClassFixture<BatchingSqlAzureFixture>
    {
        private readonly BatchingSqlAzureFixture _fixture;
        private readonly ITestOutputHelper _output;

        public SqlAzureBatchingTest(BatchingSqlAzureFixture fixture, ITestOutputHelper output)
        {
            _output = output;
            _fixture = fixture;
        }

        // TODO #4228 
        // [ConditionalTheory]
        [InlineData(2)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public void AddWithBatchSize(int batchSize)
        {
            using (var context = _fixture.CreateContext(batchSize))
            {
                using (var transaction = context.Database.BeginTransaction())
                {
                    for (var i = 0; i < batchSize; i++)
                    {
                        var uuid = Guid.NewGuid().ToString();
                        context.Products.Add(new Product
                        {
                            Name = uuid,
                            ProductNumber = uuid.Substring(0, 25),
                            Weight = 0.01m,
                            SellStartDate = DateTime.Now
                        });
                    }

                    Assert.Equal(batchSize, context.SaveChanges());
                    transaction.Rollback();
                }
            }
        }
    }
}
