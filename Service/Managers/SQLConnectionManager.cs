using System;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace OSDC.Drilling.SurveyInstrument.Service.Managers
{
    /// <summary>
    /// A manager for the sql database connection, registered as a singleton through dependency injection (see Program.cs)
    /// Prior to creating a database, existing database structure is checked for consistency with the structure defined in tableStructureDict_
    /// If inconsistent (table count, table names, fields count, fields names), a timestamped backup of the existing database is generated first
    /// </summary>
    /// <remarks>
    /// SQLite database connection strategy:
    /// - single connection for every access (chosen strategy in the general case)
    ///     each access to the database is performed through isolated connections stored in a List of connections
    ///     > isolation, reliability, fail-safe, thread-safe, but overhead due to opening connections
    /// - shared connection between access
    ///     one connection is opened for the lifetime of the application and used to access database through various web requests and commands 
    ///     > no overhead, but issues with concurrency, single-point of failure, state management
    /// - scoped connection (registering service with AddScoped rather than AddSingleton)
    ///     one connection is opened per web request
    ///     > same problems as with shared connection, but limited to the scope of one webrequest rather than to the whole lifetime of the application
    /// </remarks>
    public class SqlConnectionManager
    {
        private readonly ILogger<SqlConnectionManager> _logger;
        private readonly string _connectionString;
        public static readonly string HOME_DIRECTORY = ".." + Path.DirectorySeparatorChar + "home" + Path.DirectorySeparatorChar;
        public static readonly string DATABASE_FILENAME = "SurveyInstrument.db";
        public static readonly string DATE_TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";
        public const int CURRENT_SCHEMA_VERSION = 2;

        // dictionary describing tables format
        // Light weight data fields are enumerated explicitly in the data table implementing the light weight data concept
        // (thus duplicating info in the database) for 2 reasons
        // 1) to avoid loading the complete SurveyInstrument (heavy weight data) each time we only need contextual info on the data (light weight data)
        // 2) to keep control of the logic of inserting and selecting a light data in the database
        //    localized at the controller/manager level (storing SurveyInstrumentLight as a whole could induce database corruption issues)
        // If the light weight data concept is not implemented, the same contextual info can be retrieved directly from the SurveyInstrument
        private readonly static Dictionary<string, string[]> _tableStructureDict = new Dictionary<string, string[]>()
            {
                { "ErrorSourceTable", new string[] {
                    "ID text primary key",
                    "MetaInfo text",
                    "ErrorSource text" }
                },
                { "SurveyInstrumentTable", new string[] {
                    "ID text primary key",
                    "MetaInfo text",
                    // beginning of list of fields used only when light weight concept is implemented
                    "Name text",
                    "Description text",
                    // end of list of fields used only when light weight concept is implemented
                    "CreationDate text",
                    "LastModificationDate text",
                    "SurveyInstrument text" }
                },
                { "SurveyInstrumentIdentityTable", new string[] {
                    "ID text primary key",
                    "MetaInfo text",
                    "Name text",
                    "CreationDate text",
                    "LastModificationDate text",
                    "SurveyInstrumentIdentity text" }
                },
                { "SurveyInstrumentFeatureCategoryTable", new string[] {
                    "ID text primary key",
                    "MetaInfo text",
                    "Name text",
                    "IsExclusive integer",
                    "HasValidityPeriod integer",
                    "CreationDate text",
                    "LastModificationDate text",
                    "SurveyInstrumentFeatureCategory text" }
                }
            };

        public SqlConnectionManager(string connectionString, ILogger<SqlConnectionManager> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
            _logger.LogInformation("SqliteConnectionManager created");
            if (Initialize())
            {
                ManageDataBase();
            }
            else
            {
                _logger.LogInformation("SqliteConnectionManager created");
            }
        }

        public SqliteConnection? GetConnection()
        {
            // a new SQL connection is opened for every transaction, thus ensuring thread-safety and removing unnecessary locks
            var connection = new SqliteConnection(_connectionString);
            if (connection != null)
            {
                connection.Open();
            }
            else
            {
                _logger.LogError("Problem while opening SQLite connection");
            }
            return connection;
        }

        private bool Initialize()
        {
            if (!Directory.Exists(HOME_DIRECTORY))
            {
                _logger.LogInformation("Creating home directory");
                try
                {
                    Directory.CreateDirectory(HOME_DIRECTORY);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Impossible to create home directory for local storage");
                    return false;
                }
            }
            if (Directory.Exists(HOME_DIRECTORY))
            {
                try
                {
                    string databaseFileName = HOME_DIRECTORY + Path.DirectorySeparatorChar + DATABASE_FILENAME;
                    if (File.Exists(databaseFileName))
                    {
                        _logger.LogInformation("Opening database {_databaseFileName}", DATABASE_FILENAME);
                    }
                    else
                    {
                        _logger.LogInformation("Creating database {_databaseFileName}", DATABASE_FILENAME);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Impossible to create {_databaseFileName}", DATABASE_FILENAME);
                    return false;
                }
            }
            else
            {
                _logger.LogError("Home directory for local storage should have been created, check for access");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Creates an empty database transactionally or adopts the unchanged legacy schema by setting its version marker.
        /// Existing tables and rows are never dropped, renamed, or rebuilt automatically.
        /// </summary>
        private void ManageDataBase()
        {
            using SqliteConnection connection = GetConnection()
                ?? throw new InvalidOperationException("Unable to open the SurveyInstrument database.");

            List<string> tableNames = [];
            using (SqliteCommand tables = connection.CreateCommand())
            {
                tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                using SqliteDataReader reader = tables.ExecuteReader();
                while (reader.Read())
                {
                    tableNames.Add(reader.GetString(0));
                }
            }

            using SqliteCommand versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "PRAGMA user_version";
            int schemaVersion = Convert.ToInt32(versionCommand.ExecuteScalar());
            if (schemaVersion > CURRENT_SCHEMA_VERSION)
            {
                throw new InvalidOperationException($"SurveyInstrument database schema version {schemaVersion} is newer than supported version {CURRENT_SCHEMA_VERSION}.");
            }

            if (tableNames.Count == 0)
            {
                if (schemaVersion != 0)
                {
                    throw new InvalidOperationException("The versioned SurveyInstrument database has no tables. No data was changed.");
                }

                using SqliteTransaction transaction = connection.BeginTransaction();
                try
                {
                    foreach (KeyValuePair<string, string[]> table in _tableStructureDict)
                    {
                        CreateTable(connection, transaction, table);
                    }
                    SetSchemaVersion(connection, transaction);
                    transaction.Commit();
                    tableNames = _tableStructureDict.Keys.ToList();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
                return;
            }

            List<string> unexpected = tableNames.Except(_tableStructureDict.Keys, StringComparer.Ordinal).ToList();
            if (unexpected.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Unexpected SurveyInstrument database tables. No data was changed: [{string.Join(',', unexpected)}].");
            }

            string[] legacyTables = ["ErrorSourceTable", "SurveyInstrumentTable"];
            List<string> missingLegacy = legacyTables.Except(tableNames, StringComparer.Ordinal).ToList();
            List<string> malformed = _tableStructureDict
                .Where(table => tableNames.Contains(table.Key, StringComparer.Ordinal) &&
                    !CheckDatabaseStructure(connection, table))
                .Select(table => table.Key)
                .ToList();
            if (missingLegacy.Count > 0 || malformed.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Unexpected SurveyInstrument database structure. No data was changed. Missing=[{string.Join(',', missingLegacy)}], malformed=[{string.Join(',', malformed)}].");
            }

            if (schemaVersion < CURRENT_SCHEMA_VERSION)
            {
                using SqliteTransaction transaction = connection.BeginTransaction();
                try
                {
                    foreach (KeyValuePair<string, string[]> table in _tableStructureDict)
                    {
                        if (!tableNames.Contains(table.Key, StringComparer.Ordinal))
                        {
                            CreateTable(connection, transaction, table);
                        }
                        else
                        {
                            CreateIndex(connection, transaction, table.Key);
                        }
                    }
                    SetSchemaVersion(connection, transaction);
                    transaction.Commit();
                    tableNames = _tableStructureDict.Keys.ToList();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }

            List<string> finalMissing = _tableStructureDict.Keys.Except(tableNames, StringComparer.Ordinal).ToList();
            List<string> finalMalformed = _tableStructureDict
                .Where(table => tableNames.Contains(table.Key, StringComparer.Ordinal) &&
                    !CheckDatabaseStructure(connection, table))
                .Select(table => table.Key)
                .ToList();
            if (finalMissing.Count > 0 || finalMalformed.Count > 0)
            {
                throw new InvalidOperationException(
                    $"SurveyInstrument database schema is incomplete. Missing=[{string.Join(',', finalMissing)}], malformed=[{string.Join(',', finalMalformed)}].");
            }
        }

        /// <summary>
        /// Check that expected fields (in tableStructure.Value) exactly match those of the stored database
        /// </summary>
        /// <param name="tableStructure"></param>
        /// <returns>true if the expected fields exactly match fields of the stored database</returns>
        private static bool CheckDatabaseStructure(SqliteConnection connection, KeyValuePair<string, string[]> tableStructure)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM {tableStructure.Key}";
            using SqliteDataReader reader = command.ExecuteReader(CommandBehavior.SchemaOnly);
            DataTable? schema = reader.GetSchemaTable();
            if (schema == null || tableStructure.Value.Length != schema.Rows.Count)
            {
                return false;
            }
            foreach (string field in tableStructure.Value)
            {
                string expectedName = field.Split(' ')[0];
                if (!schema.Rows.Cast<DataRow>().Any(column => column.Field<string>("ColumnName") == expectedName))
                {
                    return false;
                }
            }
            return true;
        }

        private static void CreateTable(SqliteConnection connection, SqliteTransaction transaction, KeyValuePair<string, string[]> table)
        {
            using SqliteCommand create = connection.CreateCommand();
            create.Transaction = transaction;
            create.CommandText = $"CREATE TABLE {table.Key} ({string.Join(',', table.Value)})";
            create.ExecuteNonQuery();
            CreateIndex(connection, transaction, table.Key);
        }

        private static void CreateIndex(SqliteConnection connection, SqliteTransaction transaction, string tableName)
        {
            using SqliteCommand index = connection.CreateCommand();
            index.Transaction = transaction;
            index.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS {tableName}Index ON {tableName} (ID)";
            index.ExecuteNonQuery();
        }

        private static void SetSchemaVersion(SqliteConnection connection, SqliteTransaction transaction)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"PRAGMA user_version = {CURRENT_SCHEMA_VERSION}";
            command.ExecuteNonQuery();
        }
    }
}
