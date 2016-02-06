// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.EntityFrameworkCore.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Scaffolding
{
    public class SqlServerDatabaseModelFactory : IDatabaseModelFactory
    {
        private SqlConnection _connection;
        private TableSelectionSet _tableSelectionSet;
        private DatabaseModel _databaseModel;
        private Dictionary<string, TableModel> _tables;
        private Dictionary<string, ColumnModel> _tableColumns;

        private static string TableKey(TableModel table) => TableKey(table.Name, table.SchemaName);
        private static string TableKey(string name, string schema = null) => "[" + (schema ?? "") + "].[" + name + "]";
        private static string ColumnKey(TableModel table, string columnName) => TableKey(table) + ".[" + columnName + "]";

        private static readonly ISet<string> _dateTimePrecisionTypes = new HashSet<string> { "datetimeoffset", "datetime2", "time" };
        private const int DefaultDateTimePrecision = 7;
        // see https://msdn.microsoft.com/en-us/library/ff878091.aspx
        private static readonly Dictionary<string, long[]> _defaultSequenceMinMax = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "tinyint", new[] { 0L, 255L } },
            { "smallint", new[] { -32768L, 32767L } },
            { "int", new[] { -2147483648L, 2147483647L } },
            { "bigint", new[] { -9223372036854775808L, 9223372036854775807L } },
            { "decimal", new[] { -999999999999999999L, 999999999999999999L } },
            { "numeric", new[] { -999999999999999999L, 999999999999999999L } }
        };

        public SqlServerDatabaseModelFactory([NotNull] ILoggerFactory loggerFactory)
        {
            Check.NotNull(loggerFactory, nameof(loggerFactory));

            Logger = loggerFactory.CreateCommandsLogger();
        }

        public virtual ILogger Logger { get; }

        private void ResetState()
        {
            _connection = null;
            _tableSelectionSet = null;
            _databaseModel = new DatabaseModel();
            _tables = new Dictionary<string, TableModel>();
            _tableColumns = new Dictionary<string, ColumnModel>(StringComparer.OrdinalIgnoreCase);
        }

        public virtual DatabaseModel Create(string connectionString, TableSelectionSet tableSelectionSet)
        {
            Check.NotEmpty(connectionString, nameof(connectionString));
            Check.NotNull(tableSelectionSet, nameof(tableSelectionSet));

            ResetState();

            using (_connection = new SqlConnection(connectionString))
            {
                _connection.Open();
                _tableSelectionSet = tableSelectionSet;

                _databaseModel.DatabaseName = _connection.Database;

                if (SupportsSequences)
                {
                    GetSequences();
                }

                GetDefaultSchema();
                GetTypeAliases();
                GetTables();
                GetColumns();
                GetIndexes();
                GetForeignKeys();
                return _databaseModel;
            }
        }

        private bool SupportsSequences
        {
            get
            {
                Version v;
                if (Version.TryParse(_connection.ServerVersion, out v))
                {
                    return v.Major >= 11;
                }
                return false;
            }
        }

        private void GetDefaultSchema()
        {
            var command = _connection.CreateCommand();
            command.CommandText = "SELECT SCHEMA_NAME()";
            var schema = command.ExecuteScalar() as string ?? "dbo";
            _databaseModel.DefaultSchemaName = schema;
        }

        private void GetTypeAliases()
        {
            var command = _connection.CreateCommand();
            command.CommandText = @"SELECT
                        [type_name],
                        [underlying_system_type]
                        FROM
                        (SELECT
                          t1.[name] as [type_name],
                          ( CASE WHEN t1.[xusertype] = t1.[xtype] THEN NULL
                            ELSE
                              ( SELECT t2.[name]
                                FROM [sys].[systypes] AS t2
                                WHERE t2.[xusertype] = t2.[xtype]
                                AND t2.[xusertype] = t1.[xtype] )
                            END) as [underlying_system_type]
                          FROM [sys].[systypes] AS t1
                        ) AS t
                        WHERE [underlying_system_type] IS NOT NULL";

            var typeAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var alias = reader.GetValueOrDefault<string>("type_name");
                    var underlyingSystemType = reader.GetValueOrDefault<string>("underlying_system_type");
                    typeAliasMap.Add(alias, underlyingSystemType);
                }
            }

            _databaseModel.SqlServer().TypeAliases = typeAliasMap;
        }

        private void GetSequences()
        {
            var command = _connection.CreateCommand();
            command.CommandText = @"SELECT name,
                        is_cycling,
                        CAST(minimum_value AS bigint) as [minimum_value],
                        CAST(maximum_value AS bigint) as [maximum_value],
                        CAST(start_value AS bigint) as [start_value],
                        CAST(increment AS int) as [increment],
                        TYPE_NAME(user_type_id) as [type_name],
                        OBJECT_SCHEMA_NAME(object_id) AS [schema_name]
                        FROM sys.sequences";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var sequence = new SequenceModel
                    {
                        Database = _databaseModel,
                        SchemaName = reader.GetValueOrDefault<string>("schema_name"),
                        Name = reader.GetValueOrDefault<string>("name"),
                        DataType = reader.GetValueOrDefault<string>("type_name"),
                        IsCyclic = reader.GetValueOrDefault<bool?>("is_cycling"),
                        IncrementBy = reader.GetValueOrDefault<int?>("increment"),
                        Start = reader.GetValueOrDefault<long?>("start_value"),
                        Min = reader.GetValueOrDefault<long?>("minimum_value"),
                        Max = reader.GetValueOrDefault<long?>("maximum_value")
                    };

                    if (string.IsNullOrEmpty(sequence.Name))
                    {
                        Logger.LogWarning(SqlServerDesignStrings.SequenceNameEmpty(sequence.SchemaName));
                        continue;
                    }

                    if (_defaultSequenceMinMax.ContainsKey(sequence.DataType))
                    {
                        var defaultMin = _defaultSequenceMinMax[sequence.DataType][0];
                        sequence.Min = sequence.Min == defaultMin ? null : sequence.Min;
                        sequence.Start = sequence.Start == defaultMin ? null : sequence.Start;

                        var defaultMax = _defaultSequenceMinMax[sequence.DataType][1];
                        sequence.Max = sequence.Max == defaultMax ? null : sequence.Max;
                    }

                    _databaseModel.Sequences.Add(sequence);
                }
            }
        }

        private void GetTables()
        {
            var command = _connection.CreateCommand();
            command.CommandText =
                "SELECT schema_name(t.schema_id) AS [schema], t.name FROM sys.tables AS t " +
                $"WHERE t.name <> '{HistoryRepository.DefaultTableName}'";
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var table = new TableModel
                    {
                        Database = _databaseModel,
                        SchemaName = reader.GetValueOrDefault<string>("schema"),
                        Name = reader.GetValueOrDefault<string>("name")
                    };

                    if (_tableSelectionSet.Allows(table.SchemaName, table.Name))
                    {
                        _databaseModel.Tables.Add(table);
                        _tables[TableKey(table)] = table;
                    }
                }
            }
        }

        private void GetColumns()
        {
            var command = _connection.CreateCommand();
            command.CommandText = @"SELECT DISTINCT
    schema_name(t.schema_id) AS [schema],
    t.name AS [table], 
    type_name(c.user_type_id) AS [typename],
    c.name AS [column_name],
    c.column_id AS [ordinal],
    c.is_nullable AS [nullable],
    CAST(ic.key_ordinal AS int) AS [primary_key_ordinal],
    object_definition(c.default_object_id) AS [default_sql],
    CAST(CASE WHEN c.precision <> tp.precision
            THEN c.precision
            ELSE null
        END AS int) AS [precision],
    CAST(CASE WHEN c.scale <> tp.scale
            THEN c.scale
            ELSE null
        END AS int) AS [scale],
    CAST(CASE WHEN c.max_length <> tp.max_length
            THEN c.max_length
            ELSE null
        END AS int) AS [max_length],
    c.is_identity,
    c.is_computed
FROM sys.index_columns ic
    RIGHT JOIN (SELECT * FROM sys.indexes WHERE is_primary_key = 1) AS i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    RIGHT JOIN sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id
    RIGHT JOIN sys.types tp ON tp.user_type_id = c.user_type_id
    JOIN sys.tables AS t ON t.object_id = c.object_id
WHERE t.name <> '" + HistoryRepository.DefaultTableName + "'";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var schemaName = reader.GetValueOrDefault<string>("schema");
                    var tableName = reader.GetValueOrDefault<string>("table");
                    var columnName = reader.GetValueOrDefault<string>("column_name");

                    if (!_tableSelectionSet.Allows(schemaName, tableName))
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(columnName))
                    {
                        Logger.LogWarning(SqlServerDesignStrings.ColumnNameEmptyOnTable(schemaName, tableName));
                        continue;
                    }

                    TableModel table;
                    if (!_tables.TryGetValue(TableKey(tableName, schemaName), out table))
                    {
                        Logger.LogWarning(
                            SqlServerDesignStrings.UnableToFindTableForColumn(columnName, schemaName, tableName));
                        continue;
                    }

                    var dataTypeName = reader.GetValueOrDefault<string>("typename");
                    var nullable = reader.GetValueOrDefault<bool>("nullable");
                    var scale = reader.GetValueOrDefault<int?>("scale");
                    var maxLength = reader.GetValueOrDefault<int?>("max_length");
                    var dateTimePrecision = default(int?);

                    if (dataTypeName == "nvarchar"
                        || dataTypeName == "nchar")
                    {
                        maxLength /= 2;
                    }

                    if (dataTypeName == "decimal"
                        || dataTypeName == "numeric")
                    {
                        // maxlength here represents storage bytes. The server determines this, not the client.
                        maxLength = null;
                    }

                    if (_dateTimePrecisionTypes.Contains(dataTypeName))
                    {
                        dateTimePrecision = scale ?? DefaultDateTimePrecision;
                        scale = null;
                    }

                    var isIdentity = reader.GetValueOrDefault<bool>("is_identity");
                    var isComputed = reader.GetValueOrDefault<bool>("is_computed") || dataTypeName == "timestamp";

                    var column = new ColumnModel
                    {
                        Table = table,
                        DataType = dataTypeName,
                        Name = columnName,
                        Ordinal = reader.GetValueOrDefault<int>("ordinal") - 1,
                        IsNullable = nullable,
                        PrimaryKeyOrdinal = reader.GetValueOrDefault<int?>("primary_key_ordinal"),
                        DefaultValue = reader.GetValueOrDefault<string>("default_sql"),
                        Precision = reader.GetValueOrDefault<int?>("precision"),
                        Scale = scale,
                        MaxLength = maxLength <= 0 ? default(int?) : maxLength,
                        ValueGenerated = isIdentity
                            ? ValueGenerated.OnAdd
                            : isComputed
                                ? ValueGenerated.OnAddOrUpdate
                                : default(ValueGenerated?)
                    };
                    column.SqlServer().IsIdentity = isIdentity;
                    column.SqlServer().DateTimePrecision = dateTimePrecision;

                    table.Columns.Add(column);
                    _tableColumns.Add(ColumnKey(table, column.Name), column);
                }
            }
        }

        private void GetIndexes()
        {
            var command = _connection.CreateCommand();
            command.CommandText = @"SELECT
    object_schema_name(i.object_id) AS [schema_name],
    object_name(i.object_id) AS [table_name],
    i.name AS [index_name],
    i.is_unique,
    c.name AS [column_name],
    i.type_desc,
    ic.key_ordinal
FROM sys.indexes i
    INNER JOIN sys.index_columns ic  ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id
    INNER JOIN sys.tables t ON t.object_id = i.object_id
WHERE object_schema_name(i.object_id) <> 'sys'
    AND object_name(i.object_id) <> '" + HistoryRepository.DefaultTableName + @"'
ORDER BY object_schema_name(i.object_id), object_name(i.object_id), i.name, ic.key_ordinal";

            using (var reader = command.ExecuteReader())
            {
                IndexModel index = null;
                while (reader.Read())
                {
                    var schemaName = reader.GetValueOrDefault<string>("schema_name");
                    var tableName = reader.GetValueOrDefault<string>("table_name");
                    var indexName = reader.GetValueOrDefault<string>("index_name");

                    if (!_tableSelectionSet.Allows(schemaName, tableName))
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(indexName))
                    {
                        Logger.LogWarning(SqlServerDesignStrings.IndexNameEmpty(schemaName, tableName));
                        continue;
                    }

                    if (index == null
                        || index.Name != indexName
                        || index.Table.Name != tableName
                        || index.Table.SchemaName != schemaName)
                    {
                        TableModel table;
                        if (!_tables.TryGetValue(TableKey(tableName, schemaName), out table))
                        {
                            Logger.LogWarning(
                                SqlServerDesignStrings.UnableToFindTableForIndex(indexName, schemaName, tableName));
                            continue;
                        }

                        index = new IndexModel
                        {
                            Table = table,
                            Name = indexName,
                            IsUnique = reader.GetValueOrDefault<bool>("is_unique")
                        };
                        index.SqlServer().IsClustered = reader.GetValueOrDefault<string>("type_desc") == "CLUSTERED";

                        table.Indexes.Add(index);
                    }

                    var columnName = reader.GetValueOrDefault<string>("column_name");
                    var indexOrdinal = reader.GetValueOrDefault<byte>("key_ordinal");

                    ColumnModel column;
                    if (string.IsNullOrEmpty(columnName))
                    {
                        Logger.LogWarning(
                            SqlServerDesignStrings.ColumnNameEmptyOnIndex(
                                schemaName, tableName, indexName));
                    }
                    else if (!_tableColumns.TryGetValue(ColumnKey(index.Table, columnName), out column))
                    {
                        Logger.LogWarning(
                            SqlServerDesignStrings.UnableToFindColumnForIndex(
                                indexName, columnName, schemaName, tableName));
                    }
                    else
                    {
                        var indexColumn = new IndexColumnModel
                        {
                            Index = index,
                            Column = column,
                            Ordinal = indexOrdinal
                        };

                        index.IndexColumns.Add(indexColumn);
                    }
                }
            }
        }

        private void GetForeignKeys()
        {
            var command = _connection.CreateCommand();
            command.CommandText = @"SELECT 
    schema_name(f.schema_id) AS [schema_name],
    object_name(f.parent_object_id) AS table_name,
    f.name AS foreign_key_name,
    object_schema_name(f.referenced_object_id) AS principal_table_schema_name,
    object_name(f.referenced_object_id) AS principal_table_name,
    col_name(fc.parent_object_id, fc.parent_column_id) AS constraint_column_name,
    col_name(fc.referenced_object_id, fc.referenced_column_id) AS referenced_column_name,
    is_disabled,
    delete_referential_action_desc,
    update_referential_action_desc,
    fc.constraint_column_id
FROM sys.foreign_keys AS f
    INNER JOIN sys.foreign_key_columns AS fc ON f.object_id = fc.constraint_object_id
ORDER BY schema_name(f.schema_id), object_name(f.parent_object_id), f.name";
            using (var reader = command.ExecuteReader())
            {
                var lastFkName = string.Empty;
                var lastFkSchemaName = string.Empty;
                var lastFkTableName = string.Empty;
                ForeignKeyModel fkInfo = null;
                while (reader.Read())
                {
                    var schemaName = reader.GetValueOrDefault<string>("schema_name");
                    var tableName = reader.GetValueOrDefault<string>("table_name");
                    var fkName = reader.GetValueOrDefault<string>("foreign_key_name");

                    if (string.IsNullOrEmpty(fkName))
                    {
                        Logger.LogWarning(SqlServerDesignStrings.ForeignKeyNameEmpty(schemaName, tableName));
                        continue;
                    }

                    if (!_tableSelectionSet.Allows(schemaName, tableName))
                    {
                        continue;
                    }

                    if (fkInfo == null
                        || lastFkSchemaName != schemaName
                        || lastFkTableName != tableName
                        || lastFkName != fkName)
                    {
                        lastFkName = fkName;
                        lastFkSchemaName = schemaName;
                        lastFkTableName = tableName;
                        var table = _tables[TableKey(tableName, schemaName)];

                        var principalSchemaTableName = reader.GetValueOrDefault<string>("principal_table_schema_name");
                        var principalTableName = reader.GetValueOrDefault<string>("principal_table_name");
                        TableModel principalTable = null;
                        if (!string.IsNullOrEmpty(principalSchemaTableName)
                            && !string.IsNullOrEmpty(principalTableName))
                        {
                            _tables.TryGetValue(TableKey(principalTableName, principalSchemaTableName), out principalTable);
                        }

                        fkInfo = new ForeignKeyModel
                        {
                            Name = fkName,
                            Table = table,
                            PrincipalTable = principalTable,
                            OnDelete = ConvertToReferentialAction(reader.GetValueOrDefault<string>("delete_referential_action_desc"))
                        };

                        table.ForeignKeys.Add(fkInfo);
                    }

                    var fkColumn = new ForeignKeyColumnModel
                    {
                        Ordinal = reader.GetValueOrDefault<int>("constraint_column_id")
                    };

                    var fromColumnName = reader.GetValueOrDefault<string>("constraint_column_name");

                    ColumnModel fromColumn;
                    if ((fromColumn = FindColumnForForeignKey(fromColumnName, fkInfo.Table, fkName)) != null)
                    {
                        fkColumn.Column = fromColumn;
                    }

                    if (fkInfo.PrincipalTable != null)
                    {
                        var toColumnName = reader.GetValueOrDefault<string>("referenced_column_name");
                        ColumnModel toColumn;
                        if ((toColumn = FindColumnForForeignKey(toColumnName, fkInfo.PrincipalTable, fkName)) != null)
                        {
                            fkColumn.PrincipalColumn = toColumn;
                        }
                    }

                    fkInfo.Columns.Add(fkColumn);
                }
            }
        }

        private ColumnModel FindColumnForForeignKey(
            string columnName, TableModel table, string fkName)
        {
            ColumnModel column;
            if (string.IsNullOrEmpty(columnName))
            {
                Logger.LogWarning(
                    SqlServerDesignStrings.ColumnNameEmptyOnForeignKey(
                        table.SchemaName, table.Name, fkName));
                return null;
            }

            if (!_tableColumns.TryGetValue(
                ColumnKey(table, columnName), out column))
            {
                Logger.LogWarning(
                    SqlServerDesignStrings.UnableToFindColumnForForeignKey(
                        fkName, columnName, table.SchemaName, table.Name));
                return null;
            }

            return column;
        }

        private static ReferentialAction? ConvertToReferentialAction(string onDeleteAction)
        {
            switch (onDeleteAction.ToUpperInvariant())
            {
                case "RESTRICT":
                    return ReferentialAction.Restrict;

                case "CASCADE":
                    return ReferentialAction.Cascade;

                case "SET_NULL":
                    return ReferentialAction.SetNull;

                case "SET_DEFAULT":
                    return ReferentialAction.SetDefault;

                case "NO_ACTION":
                    return ReferentialAction.NoAction;

                default:
                    return null;
            }
        }
    }
}
