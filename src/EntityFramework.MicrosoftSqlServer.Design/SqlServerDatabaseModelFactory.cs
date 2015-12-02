﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations;
using Microsoft.Data.Entity.Scaffolding.Internal;
using Microsoft.Data.Entity.Scaffolding.Metadata;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.Data.Entity.Scaffolding
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
                 // TODO actually load per-user
                _databaseModel.DefaultSchemaName = "dbo";

                GetTables();
                GetColumns();
                GetIndexes();
                GetForeignKeys();
                return _databaseModel;
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
                        SchemaName = reader.GetString(0),
                        Name = reader.GetString(1)
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
                    var schemaName = reader.GetString(0);
                    var tableName = reader.GetString(1);
                    var columnName = reader.GetStringOrNull(3);
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

                    var dataTypeName = reader.GetString(2);
                    var nullable = reader.IsDBNull(5) ? false : reader.GetBoolean(5);

                    var maxLength = reader.IsDBNull(10) ? default(int?) : reader.GetInt32(10);

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

                    var isIdentity = !reader.IsDBNull(11) && reader.GetBoolean(11);
                    var isComputed = reader.GetBoolean(12) || dataTypeName == "timestamp";

                    var column = new ColumnModel
                    {
                        Table = table,
                        DataType = dataTypeName,
                        Name = columnName,
                        Ordinal = reader.GetInt32(4) - 1,
                        IsNullable = nullable,
                        PrimaryKeyOrdinal = reader.IsDBNull(6) ? default(int?) : reader.GetInt32(6),
                        DefaultValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Precision = reader.IsDBNull(8) ? default(int?) : reader.GetInt32(8),
                        Scale = reader.IsDBNull(9) ? default(int?) : reader.GetInt32(9),
                        MaxLength = maxLength <= 0 ? default(int?) : maxLength,
                        IsIdentity = isIdentity,
                        ValueGenerated = isIdentity ?
                            ValueGenerated.OnAdd :
                            isComputed ?
                                ValueGenerated.OnAddOrUpdate : default(ValueGenerated?)
                    };

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
    i.type_desc
FROM sys.indexes i
    INNER JOIN sys.index_columns ic  ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id
WHERE object_schema_name(i.object_id) <> 'sys'
    AND i.is_primary_key <> 1
    AND object_name(i.object_id) <> '" + HistoryRepository.DefaultTableName + @"'
ORDER BY object_schema_name(i.object_id), object_name(i.object_id), i.name, ic.key_ordinal";

            using (var reader = command.ExecuteReader())
            {
                IndexModel index = null;
                while (reader.Read())
                {
                    var schemaName = reader.GetString(0);
                    var tableName = reader.GetString(1);
                    var indexName = reader.GetStringOrNull(2);
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
                        if(!_tables.TryGetValue(TableKey(tableName, schemaName), out table))
                        {
                            Logger.LogWarning(
                                SqlServerDesignStrings.UnableToFindTableForIndex(indexName, schemaName, tableName));
                            continue;
                        }

                        index = new IndexModel
                        {
                            Table = table,
                            Name = indexName,
                            IsUnique = reader.IsDBNull(3) ? false : reader.GetBoolean(3),
                            IsClustered =
                                (!reader.IsDBNull(5) && reader.GetString(5) == "CLUSTERED")
                                ? true
                                : default(bool?)
                        };
                        table.Indexes.Add(index);
                    }

                    var columnName = reader.GetStringOrNull(4);
                    ColumnModel column = null;
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
                        index.Columns.Add(column);
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
    update_referential_action_desc
FROM sys.foreign_keys AS f
    INNER JOIN sys.foreign_key_columns AS fc ON f.object_id = fc.constraint_object_id
ORDER BY schema_name(f.schema_id), object_name(f.parent_object_id), f.name, fc.constraint_column_id";
            using (var reader = command.ExecuteReader())
            {
                var lastFkName = string.Empty;
                var lastFkSchemaName = string.Empty;
                var lastFkTableName = string.Empty;
                ForeignKeyModel fkInfo = null;
                while (reader.Read())
                {
                    var schemaName = reader.GetString(0);
                    var tableName = reader.GetString(1);
                    var fkName = reader.GetStringOrNull(2);
                    if (string.IsNullOrEmpty(fkName))
                    {
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

                        var principalSchemaTableName = reader.GetStringOrNull(3);
                        var principalTableName = reader.GetStringOrNull(4);
                        TableModel principalTable = null;
                        if (!string.IsNullOrEmpty(principalSchemaTableName)
                            && !string.IsNullOrEmpty(principalTableName))
                        {
                            _tables.TryGetValue(TableKey(principalTableName, principalSchemaTableName), out principalTable);
                        }

                        fkInfo = new ForeignKeyModel
                        {
                            Table = table,
                            PrincipalTable = principalTable
                        };

                        table.ForeignKeys.Add(fkInfo);
                    }

                    var fromColumnName = reader.GetStringOrNull(5);
                    ColumnModel fromColumn;
                    if ((fromColumn = FindColumnForForeignKey(fromColumnName, fkInfo.Table, fkName)) != null)
                    {
                        fkInfo.Columns.Add(fromColumn);
                    }

                    if (fkInfo.PrincipalTable != null)
                    {
                        var toColumnName = reader.GetString(6);
                        ColumnModel toColumn;
                        if ((toColumn = FindColumnForForeignKey(toColumnName, fkInfo.PrincipalTable, fkName)) != null)
                        {
                            fkInfo.PrincipalColumns.Add(toColumn);
                        }
                    }

                    fkInfo.OnDelete = ConvertToReferentialAction(reader.GetStringOrNull(8));
                }
            }
        }

        private ColumnModel FindColumnForForeignKey(
            string columnName, TableModel table, string fkName)
        {
            ColumnModel column = null;
            if (string.IsNullOrEmpty(columnName))
            {
                Logger.LogWarning(
                    SqlServerDesignStrings.ColumnNameEmptyOnForeignKey(
                        table.SchemaName, table.Name, fkName));
                return null;
            }
            else if (!_tableColumns.TryGetValue(
                ColumnKey(table, columnName), out column))
            {
                Logger.LogWarning(
                    SqlServerDesignStrings.UnableToFindColumnForForeignKey(
                        fkName, columnName, table.SchemaName, table.Name));
                return null;
            }
            else
            {
                return column;
            }
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
