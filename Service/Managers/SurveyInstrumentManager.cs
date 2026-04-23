using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OSDC.DotnetLibraries.General.DataManagement;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using NORCE.Drilling.SurveyInstrument.Model;
using OSDC.DotnetLibraries.Drilling.Surveying;

namespace NORCE.Drilling.SurveyInstrument.Service.Managers
{
    /// <summary>
    /// A manager for SurveyInstrument. The manager implements the singleton pattern as defined by 
    /// Gamma, Erich, et al. "Design patterns: Abstraction and reuse of object-oriented design." 
    /// European Conference on Object-Oriented Programming. Springer, Berlin, Heidelberg, 1993.
    /// </summary>
    public class SurveyInstrumentManager
    {
        private static SurveyInstrumentManager? _instance = null;
        private readonly ILogger<SurveyInstrumentManager> _logger;
        private readonly SqlConnectionManager _connectionManager;
        private readonly ErrorSourceManager _errorSourceManager;
        private static readonly double DEG2RAD = Math.PI / 180.0;

        private SurveyInstrumentManager(ILogger<SurveyInstrumentManager> logger, ILogger<ErrorSourceManager> errorSourceLogger, SqlConnectionManager connectionManager)
        {
            _logger = logger;
            _connectionManager = connectionManager;
            _errorSourceManager = ErrorSourceManager.GetInstance(errorSourceLogger, connectionManager);

            // make sure database contains default SurveyInstruments
            List<Guid>? ids = GetAllSurveyInstrumentId();
            if (ids != null && ids.Count < 1)
            {
                FillDefault();
            }
            ids = GetAllSurveyInstrumentId();
            if (ids != null)
            {
                bool isCorrupted = false;
                try
                {
                    foreach (Guid id in ids)
                    {
                        OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument? si = GetSurveyInstrumentById(id);
                        if (si == null || si.MetaInfo == null || si.MetaInfo.ID != id)
                        {
                            isCorrupted = true;
                            break;
                        }
                    }

                }
                catch (Exception ex)
                {
                    isCorrupted = true;
                }
                if (isCorrupted)
                {
                    _logger.LogWarning("The SurveyInstrumentTable is corrupted: clearing it and filling it with default SurveyInstruments");
                    Clear();
                    FillDefault();
                }
            }
        }

        public static SurveyInstrumentManager GetInstance(ILogger<SurveyInstrumentManager> logger, ILogger<ErrorSourceManager> errorSourceLogger, SqlConnectionManager connectionManager)
        {
            _instance ??= new SurveyInstrumentManager(logger, errorSourceLogger, connectionManager);
            return _instance;
        }

        public int Count
        {
            get
            {
                int count = 0;
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM SurveyInstrumentTable";
                    try
                    {
                        using SqliteDataReader reader = command.ExecuteReader();
                        if (reader.Read())
                        {
                            count = (int)reader.GetInt64(0);
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to count records in the SurveyInstrumentTable");
                    }
                }
                else
                {
                    _logger.LogWarning("Impossible to access the SQLite database");
                }
                return count;
            }
        }

        public bool Clear()
        {
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                bool success = false;
                using var transaction = connection.BeginTransaction();
                try
                {
                    //empty SurveyInstrumentTable
                    var command = connection.CreateCommand();
                    command.CommandText = "DELETE FROM SurveyInstrumentTable";
                    command.ExecuteNonQuery();

                    transaction.Commit();
                    success = true;
                }
                catch (SqliteException ex)
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "Impossible to clear the SurveyInstrumentTable");
                }
                return success;
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
                return false;
            }
        }

        public bool Contains(Guid guid)
        {
            int count = 0;
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM SurveyInstrumentTable WHERE ID = '{guid}'";
                try
                {
                    using SqliteDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        count = (int)reader.GetInt64(0);
                    }
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Impossible to count rows from SurveyInstrumentTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return count >= 1;
        }

        /// <summary>
        /// Returns the list of Guid of all SurveyInstrument present in the microservice database 
        /// </summary>
        /// <returns>the list of Guid of all SurveyInstrument present in the microservice database</returns>
        public List<Guid>? GetAllSurveyInstrumentId()
        {
            List<Guid> ids = [];
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT ID FROM SurveyInstrumentTable";
                try
                {
                    using var reader = command.ExecuteReader();
                    while (reader.Read() && !reader.IsDBNull(0))
                    {
                        Guid id = reader.GetGuid(0);
                        ids.Add(id);
                    }
                    _logger.LogInformation("Returning the list of ID of existing records from SurveyInstrumentTable");
                    return ids;
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Impossible to get IDs from SurveyInstrumentTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return null;
        }

        /// <summary>
        /// Returns the list of MetaInfo of all SurveyInstrument present in the microservice database 
        /// </summary>
        /// <returns>the list of MetaInfo of all SurveyInstrument present in the microservice database</returns>
        public List<MetaInfo?>? GetAllSurveyInstrumentMetaInfo()
        {
            List<MetaInfo?> metaInfos = new();
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT MetaInfo FROM SurveyInstrumentTable";
                try
                {
                    using var reader = command.ExecuteReader();
                    while (reader.Read() && !reader.IsDBNull(0))
                    {
                        string mInfo = reader.GetString(0);
                        MetaInfo? metaInfo = JsonSerializer.Deserialize<MetaInfo>(mInfo, JsonSettings.Options);
                        metaInfos.Add(metaInfo);
                    }
                    _logger.LogInformation("Returning the list of MetaInfo of existing records from SurveyInstrumentTable");
                    return metaInfos;
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Impossible to get IDs from SurveyInstrumentTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return null;
        }

        /// <summary>
        /// Returns the SurveyInstrument identified by its Guid from the microservice database 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>the SurveyInstrument identified by its Guid from the microservice database</returns>
        public OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument? GetSurveyInstrumentById(Guid guid)
        {
            if (!guid.Equals(Guid.Empty))
            {
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument? surveyInstrument;
                    var command = connection.CreateCommand();
                    command.CommandText = $"SELECT SurveyInstrument FROM SurveyInstrumentTable WHERE ID = '{guid}'";
                    try
                    {
                        using var reader = command.ExecuteReader();
                        if (reader.Read() && !reader.IsDBNull(0))
                        {
                            string data = reader.GetString(0);
                            surveyInstrument = JsonSerializer.Deserialize<OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument>(data, JsonSettings.Options);
                            if (surveyInstrument != null && surveyInstrument.MetaInfo != null && !surveyInstrument.MetaInfo.ID.Equals(guid))
                                throw new SqliteException("SQLite database corrupted: returned SurveyInstrument is null or has been jsonified with the wrong ID.", 1);
                        }
                        else
                        {
                            _logger.LogInformation("No SurveyInstrument of given ID in the database");
                            return null;
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to get the SurveyInstrument with the given ID from SurveyInstrumentTable");
                        return null;
                    }
                    _logger.LogInformation("Returning the SurveyInstrument of given ID from SurveyInstrumentTable");
                    return surveyInstrument;
                }
                else
                {
                    _logger.LogWarning("Impossible to access the SQLite database");
                }
            }
            else
            {
                _logger.LogWarning("The given SurveyInstrument ID is null or empty");
            }
            return null;
        }

        /// <summary>
        /// Returns the list of all SurveyInstrument present in the microservice database 
        /// </summary>
        /// <returns>the list of all SurveyInstrument present in the microservice database</returns>
        public List<OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument?>? GetAllSurveyInstrument()
        {
            List<OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument?> vals = [];
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT SurveyInstrument FROM SurveyInstrumentTable";
                try
                {
                    using var reader = command.ExecuteReader();
                    while (reader.Read() && !reader.IsDBNull(0))
                    {
                        string data = reader.GetString(0);
                        OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument? surveyInstrument = JsonSerializer.Deserialize<OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument>(data, JsonSettings.Options);
                        vals.Add(surveyInstrument);
                    }
                    _logger.LogInformation("Returning the list of existing SurveyInstrument from SurveyInstrumentTable");
                    return vals;
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Impossible to get SurveyInstrument from SurveyInstrumentTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return null;
        }

        /// <summary>
        /// Returns the list of all SurveyInstrumentLight present in the microservice database 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>the list of SurveyInstrumentLight present in the microservice database</returns>
        public List<SurveyInstrumentLight>? GetAllSurveyInstrumentLight()
        {
            List<SurveyInstrumentLight>? surveyInstrumentLightList = [];
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT MetaInfo, Name, Description, CreationDate, LastModificationDate FROM SurveyInstrumentTable";
                try
                {
                    using var reader = command.ExecuteReader();
                    while (reader.Read() && !reader.IsDBNull(0))
                    {
                        string metaInfoStr = reader.GetString(0);
                        MetaInfo? metaInfo = JsonSerializer.Deserialize<MetaInfo>(metaInfoStr, JsonSettings.Options);
                        string name = reader.GetString(1);
                        string descr = reader.GetString(2);
                        // make sure DateTimeOffset are properly instantiated when stored values are null (and parsed as empty string)
                        DateTimeOffset? creationDate = null;
                        if (DateTimeOffset.TryParse(reader.GetString(3), out DateTimeOffset cDate))
                            creationDate = cDate;
                        DateTimeOffset? lastModificationDate = null;
                        if (DateTimeOffset.TryParse(reader.GetString(4), out DateTimeOffset lDate))
                            lastModificationDate = lDate;
                        surveyInstrumentLightList.Add(new SurveyInstrumentLight(
                                metaInfo,
                                string.IsNullOrEmpty(name) ? null : name,
                                string.IsNullOrEmpty(descr) ? null : descr,
                                creationDate,
                                lastModificationDate));
                    }
                    _logger.LogInformation("Returning the list of existing SurveyInstrumentLight from SurveyInstrumentTable");
                    return surveyInstrumentLightList;
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Impossible to get light datas from SurveyInstrumentTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return null;
        }

        /// <summary>
        /// Add the given SurveyInstrument to the microservice database
        /// </summary>
        /// <param name="surveyInstrument"></param>
        /// <returns>true if the given SurveyInstrument has been added successfully to the microservice database</returns>
        public bool AddSurveyInstrument(OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument? surveyInstrument)
        {
            if (surveyInstrument != null && surveyInstrument.MetaInfo != null && surveyInstrument.MetaInfo.ID != Guid.Empty)
            {
                //update SurveyInstrumentTable
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    using SqliteTransaction transaction = connection.BeginTransaction();
                    bool success = true;
                    try
                    {
                        //add the SurveyInstrument to the SurveyInstrumentTable
                        string metaInfo = JsonSerializer.Serialize(surveyInstrument.MetaInfo, JsonSettings.Options);
                        string? cDate = null;
                        if (surveyInstrument.CreationDate != null)
                            cDate = ((DateTimeOffset)surveyInstrument.CreationDate).ToString(SqlConnectionManager.DATE_TIME_FORMAT);
                        string? lDate = null;
                        if (surveyInstrument.LastModificationDate != null)
                            lDate = ((DateTimeOffset)surveyInstrument.LastModificationDate).ToString(SqlConnectionManager.DATE_TIME_FORMAT);
                        string data = JsonSerializer.Serialize(surveyInstrument, JsonSettings.Options);
                        var command = connection.CreateCommand();
                        command.CommandText = "INSERT INTO SurveyInstrumentTable (" +
                            "ID, " +
                            "MetaInfo, " +
                            "Name, " +
                            "Description, " +
                            "CreationDate, " +
                            "LastModificationDate, " +
                            "SurveyInstrument" +
                            ") VALUES (" +
                            $"'{surveyInstrument.MetaInfo.ID}', " +
                            $"'{metaInfo}', " +
                            $"'{surveyInstrument.Name}', " +
                            $"'{surveyInstrument.Description}', " +
                            $"'{cDate}', " +
                            $"'{lDate}', " +
                            $"'{data}'" +
                            ")";
                        int count = command.ExecuteNonQuery();
                        if (count != 1)
                        {
                            _logger.LogWarning("Impossible to insert the given SurveyInstrument into the SurveyInstrumentTable");
                            success = false;
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to add the given SurveyInstrument into SurveyInstrumentTable");
                        success = false;
                    }
                    //finalizing SQL transaction
                    if (success)
                    {
                        transaction.Commit();
                        _logger.LogInformation("Added the given SurveyInstrument of given ID into the SurveyInstrumentTable successfully");
                    }
                    else
                    {
                        transaction.Rollback();
                    }
                    return success;
                }
                else
                {
                    _logger.LogWarning("Impossible to access the SQLite database");
                }
            }
            else
            {
                _logger.LogWarning("The SurveyInstrument ID or the ID of its input are null or empty");
            }
            return false;
        }

        /// <summary>
        /// Performs calculation on the given SurveyInstrument and updates it in the microservice database
        /// </summary>
        /// <param name="surveyInstrument"></param>
        /// <returns>true if the given SurveyInstrument has been updated successfully</returns>
        public bool UpdateSurveyInstrumentById(Guid guid, OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument? surveyInstrument)
        {
            bool success = true;
            if (guid != Guid.Empty && surveyInstrument != null && surveyInstrument.MetaInfo != null && surveyInstrument.MetaInfo.ID == guid)
            {
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    using SqliteTransaction transaction = connection.BeginTransaction();
                    //update fields in SurveyInstrumentTable
                    try
                    {
                        string metaInfo = JsonSerializer.Serialize(surveyInstrument.MetaInfo, JsonSettings.Options);
                        string? cDate = null;
                        if (surveyInstrument.CreationDate != null)
                            cDate = ((DateTimeOffset)surveyInstrument.CreationDate).ToString(SqlConnectionManager.DATE_TIME_FORMAT);
                        surveyInstrument.LastModificationDate = DateTimeOffset.UtcNow;
                        string? lDate = ((DateTimeOffset)surveyInstrument.LastModificationDate).ToString(SqlConnectionManager.DATE_TIME_FORMAT);
                        string data = JsonSerializer.Serialize(surveyInstrument, JsonSettings.Options);
                        var command = connection.CreateCommand();
                        command.CommandText = $"UPDATE SurveyInstrumentTable SET " +
                            $"MetaInfo = '{metaInfo}', " +
                            $"Name = '{surveyInstrument.Name}', " +
                            $"Description = '{surveyInstrument.Description}', " +
                            $"CreationDate = '{cDate}', " +
                            $"LastModificationDate = '{lDate}', " +
                            $"SurveyInstrument = '{data}' " +
                            $"WHERE ID = '{guid}'";
                        int count = command.ExecuteNonQuery();
                        if (count != 1)
                        {
                            _logger.LogWarning("Impossible to update the SurveyInstrument");
                            success = false;
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to update the SurveyInstrument");
                        success = false;
                    }

                    // Finalizing
                    if (success)
                    {
                        transaction.Commit();
                        _logger.LogInformation("Updated the given SurveyInstrument successfully");
                        return true;
                    }
                    else
                    {
                        transaction.Rollback();
                    }
                }
                else
                {
                    _logger.LogWarning("Impossible to access the SQLite database");
                }
            }
            else
            {
                _logger.LogWarning("The SurveyInstrument ID or the ID of some of its attributes are null or empty");
            }
            return false;
        }

        /// <summary>
        /// Deletes the SurveyInstrument of given ID from the microservice database
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>true if the SurveyInstrument was deleted from the microservice database</returns>
        public bool DeleteSurveyInstrumentById(Guid guid)
        {
            if (!guid.Equals(Guid.Empty))
            {
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    using var transaction = connection.BeginTransaction();
                    bool success = true;
                    //delete SurveyInstrument from SurveyInstrumentTable
                    try
                    {
                        var command = connection.CreateCommand();
                        command.CommandText = $"DELETE FROM SurveyInstrumentTable WHERE ID = '{guid}'";
                        int count = command.ExecuteNonQuery();
                        if (count < 0)
                        {
                            _logger.LogWarning("Impossible to delete the SurveyInstrument of given ID from the SurveyInstrumentTable");
                            success = false;
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to delete the SurveyInstrument of given ID from SurveyInstrumentTable");
                        success = false;
                    }
                    if (success)
                    {
                        transaction.Commit();
                        _logger.LogInformation("Removed the SurveyInstrument of given ID from the SurveyInstrumentTable successfully");
                    }
                    else
                    {
                        transaction.Rollback();
                    }
                    return success;
                }
                else
                {
                    _logger.LogWarning("Impossible to access the SQLite database");
                }
            }
            else
            {
                _logger.LogWarning("The SurveyInstrument ID is null or empty");
            }
            return false;
        }

        /// <summary>
        /// populate database with default survey instruments
        /// </summary>
        private void FillDefault()
        {
            List<OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument?> surveyInstrumentList = [
                WdWPoorMag,
                WdWGoodMag,
                WdWPoorGyro,
                WdWGoodGyro,
                MWD_ISCWSA,
                MWD_ISCWSA_Rev5_OWSG,
                Gyro_ISCWSA,
                Gyro_ISCWSA_Ex1,
                Gyro_ISCWSA_Ex2,
                Gyro_ISCWSA_Ex3,
                Gyro_ISCWSA_Ex4,
                Gyro_ISCWSA_Ex5,
                Gyro_ISCWSA_Ex6,
                SurveyInstrumentAll
                ];
            foreach (OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument? si in surveyInstrumentList)
            {
                AddSurveyInstrument(si);
            }
        }

        #region Default survey instruments

        #region SurveyInstrument WdWPoorMag
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument WdWPoorMag
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("61b6c29e-c7aa-4d68-90a0-b505a65e0d5f") },
                    Name = "WdWPoorMag",
                    Description = "Default WdWPoorMag survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.MWD_WolffDeWardt,
                    UseRelDepthError = true,
                    RelDepthError = 0.002,
                    UseMisalignment = true,
                    Misalignment = 0.3 * DEG2RAD,
                    UseTrueInclination = true,
                    TrueInclination = 1.0 * DEG2RAD,
                    UseReferenceError = true,
                    ReferenceError = 1.5 * DEG2RAD,
                    UseDrillStringMag = true,
                    DrillStringMag = 5.0 * DEG2RAD,
                    UseGyroCompassError = false,
                    GyroCompassError = null
                };
            }
        }
        #endregion

        #region SurveyInstrument WdWGoodMag
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument WdWGoodMag
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("2af52fd1-84a9-4fe0-9ea3-3d4c6256b2b5") },
                    Name = "WdWGoodMag",
                    Description = "Default WdWGoodMag survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.MWD_WolffDeWardt,
                    UseRelDepthError = true,
                    RelDepthError = 0.001,
                    UseMisalignment = true,
                    Misalignment = 0.1 * DEG2RAD,
                    UseTrueInclination = true,
                    TrueInclination = 0.5 * DEG2RAD,
                    UseReferenceError = true,
                    ReferenceError = 1.5 * DEG2RAD,
                    UseDrillStringMag = true,
                    DrillStringMag = 0.25 * DEG2RAD,
                    UseGyroCompassError = false,
                    GyroCompassError = null
                };
            }
        }
        #endregion

        #region SurveyInstrument WdWPoorGyro
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument WdWPoorGyro
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("790657d8-bdb7-4406-afe6-14f7ed0c0ecf") },
                    Name = "WdWPoorGyro",
                    Description = "Default WdWPoorGyro survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.Gyro_WolffDeWardt,
                    UseRelDepthError = true,
                    RelDepthError = 0.002,
                    UseMisalignment = true,
                    Misalignment = 0.2 * DEG2RAD,
                    UseTrueInclination = true,
                    TrueInclination = 0.5 * DEG2RAD,
                    UseReferenceError = true,
                    ReferenceError = 1.0 * DEG2RAD,
                    UseDrillStringMag = false,
                    DrillStringMag = null,
                    UseGyroCompassError = true,
                    GyroCompassError = 2.5 * DEG2RAD
                };
            }
        }
        #endregion

        #region SurveyInstrument WdWGoodGyro
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument WdWGoodGyro
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("143d9454-a2bd-4901-b4e2-7bf7888f24e5") },
                    Name = "WdWGoodGyro",
                    Description = "Default WdWGoodGyro survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.Gyro_WolffDeWardt,
                    UseRelDepthError = true,
                    RelDepthError = 0.0005,
                    UseMisalignment = true,
                    Misalignment = 0.203 * DEG2RAD,
                    UseTrueInclination = true,
                    TrueInclination = 0.2 * DEG2RAD,
                    UseReferenceError = true,
                    ReferenceError = 0.1 * DEG2RAD,
                    UseDrillStringMag = false,
                    DrillStringMag = null,
                    UseGyroCompassError = true,
                    GyroCompassError = 0.5 * DEG2RAD
                };
            }
        }
        #endregion

        #region SurveyInstrument MWD_ISCWSA
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument MWD_ISCWSA
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("3a811f1f-8b54-4952-a6a7-cf584f5e85c8") },
                    Name = "MWD_ISCWSA",
                    Description = "Default MWD_ISCWSA survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.MWD_ISCWSA,
                    UseRelDepthError = false,
                    UseMisalignment = false,
                    UseTrueInclination = false,
                    UseReferenceError = false,
                    UseDrillStringMag = false,
                    UseGyroCompassError = false,
                    ErrorSourceList = [
                        ErrorSourceFactory.Create_DRFR(magnitude:0.35),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.00056),
                        ErrorSourceFactory.Create_DSTG(magnitude:2.5e-7),
                        ErrorSourceFactory.Create_ABXY_TI1S(magnitude:0.004),
                        ErrorSourceFactory.Create_ABXY_TI2S(magnitude:0.004),
                        ErrorSourceFactory.Create_ABZ(magnitude:0.004),
                        ErrorSourceFactory.Create_ASXY_TI1S(magnitude:0.0005),
                        ErrorSourceFactory.Create_ASXY_TI2S(magnitude:0.0005),
                        ErrorSourceFactory.Create_ASXY_TI3S(magnitude:0.0005),
                        ErrorSourceFactory.Create_ASZ(magnitude:0.0005),
                        ErrorSourceFactory.Create_MBXY_TI1(magnitude:70e-9),
                        ErrorSourceFactory.Create_MBXY_TI2(magnitude:70e-9),
                        ErrorSourceFactory.Create_MBZ(magnitude:70e-9),
                        ErrorSourceFactory.Create_MSXY_TI1(magnitude:0.0016),
                        ErrorSourceFactory.Create_MSXY_TI2(magnitude:0.0016),
                        ErrorSourceFactory.Create_MSXY_TI3(magnitude:0.0016),
                        ErrorSourceFactory.Create_MSZ(magnitude:0.0016),
                        ErrorSourceFactory.Create_DEC_U(magnitude:0.16 * DEG2RAD),
                        ErrorSourceFactory.Create_DEC_OS(magnitude:0.24 * DEG2RAD),
                        ErrorSourceFactory.Create_DEC_OH(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_DEC_OI(magnitude:0.05 * DEG2RAD),
                        ErrorSourceFactory.Create_DECR(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_DBH_U(magnitude:2350e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_DBH_OS(magnitude:3359e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_DBH_OH(magnitude:2840e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_DBH_OI(magnitude:356e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_DBHR(magnitude:3000e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_AMID(magnitude:220e-6),
                        ErrorSourceFactory.Create_SAGE(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM1(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM2(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM3E(magnitude:0.3 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM4E(magnitude:0.3 * DEG2RAD),
                        ErrorSourceFactory.Create_XCLH(magnitude:0.167),
                        ErrorSourceFactory.Create_XCLL(magnitude:0.167),
                        ]
                };
            }
        }
        #endregion

        #region SurveyInstrument MWD_ISCWSA_Rev5_OWSG
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument MWD_ISCWSA_Rev5_OWSG
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("9d1a0696-22e7-4c50-b3ff-f60bd580f594") },
                    Name = "MWD_ISCWSA_Rev5_OWSG",
                    Description = "Default MWD_ISCWSA_Rev5_OWSG survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.MWD_ISCWSA,
                    UseRelDepthError = false,
                    UseMisalignment = false,
                    UseTrueInclination = false,
                    UseReferenceError = false,
                    UseDrillStringMag = false,
                    UseGyroCompassError = false,
                    ErrorSourceList = [
                        ErrorSourceFactory.Create_DRFR(magnitude:0.35),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.00056),
                        ErrorSourceFactory.Create_DSTG(magnitude:2.5e-7),
                        ErrorSourceFactory.Create_ABXY_TI1S(magnitude:0.004),
                        ErrorSourceFactory.Create_ABXY_TI2S(magnitude:0.004),
                        ErrorSourceFactory.Create_ABZ(magnitude:0.004),
                        ErrorSourceFactory.Create_ASXY_TI1S(magnitude:0.0005),
                        ErrorSourceFactory.Create_ASXY_TI2S(magnitude:0.0005),
                        ErrorSourceFactory.Create_ASXY_TI3S(magnitude:0.0005),
                        ErrorSourceFactory.Create_ASZ(magnitude:0.0005),
                        ErrorSourceFactory.Create_MBXY_TI1(magnitude:70e-9),
                        ErrorSourceFactory.Create_MBXY_TI2(magnitude:70e-9),
                        ErrorSourceFactory.Create_MBZ(magnitude:70e-9),
                        ErrorSourceFactory.Create_MSXY_TI1(magnitude:0.0016),
                        ErrorSourceFactory.Create_MSXY_TI2(magnitude:0.0016),
                        ErrorSourceFactory.Create_MSXY_TI3(magnitude:0.0016),
                        ErrorSourceFactory.Create_MSZ(magnitude:0.0016),
                        ErrorSourceFactory.Create_DEC_U(magnitude:0.16 * DEG2RAD),
                        ErrorSourceFactory.Create_DEC_OS(magnitude:0.24 * DEG2RAD),
                        ErrorSourceFactory.Create_DEC_OH(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_DEC_OI(magnitude:0.05 * DEG2RAD),
                        ErrorSourceFactory.Create_DECR(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_DBH_U(magnitude:2350e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_DBH_OS(magnitude:3359e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_DBH_OH(magnitude:2840e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_DBH_OI(magnitude:356e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_DBHR(magnitude:3000e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_AMID(magnitude:220e-6),
                        ErrorSourceFactory.Create_SAGE(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM1(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM2(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM3E(magnitude:0.3 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM4E(magnitude:0.3 * DEG2RAD),
                        ErrorSourceFactory.Create_XCLH(magnitude:0.167),
                        ErrorSourceFactory.Create_XCLL(magnitude:0.167),
                        ]
                };
            }
        }
        #endregion

        #region SurveyInstrument Gyro_ISCWSA
        private static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument? _gyro_ISCWSA = null;
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument Gyro_ISCWSA
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("8ee3d202-47d3-40b2-a8e1-29a4605a025f") },
                    Name = "Gyro_ISCWSA",
                    Description = "Default Gyro_ISCWSA survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.Gyro_ISCWSA,
                    UseRelDepthError = false,
                    UseMisalignment = false,
                    UseTrueInclination = false,
                    UseReferenceError = false,
                    UseDrillStringMag = false,
                    UseGyroCompassError = false,
                    CantAngle = 0.0 * DEG2RAD,
                    GyroSwitching = 1,
                    GyroNoiseRed = 1.0,
                    GyroMinDist = 9999,
                    ErrorSourceList = [
                        ErrorSourceFactory.Create_AXY_B(magnitude:0.005),
                        ErrorSourceFactory.Create_AXY_SF(magnitude:0.0005),
                        ErrorSourceFactory.Create_AXY_MS(magnitude:0.05 * DEG2RAD),
                        ErrorSourceFactory.Create_AXY_GB(magnitude:0.005),
                        ErrorSourceFactory.Create_GXY_B1(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_B2(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_RN(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G1(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G2(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G3(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G4(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_SF(magnitude:0.001, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_MIS(magnitude:0.05 * DEG2RAD, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM1(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM2(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM3(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM4(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_SAG(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_DRFR(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.001),
                        ErrorSourceFactory.Create_DSTG(magnitude:5.0e-7),
                        ]
                };
            }
        }
        #endregion

        #region SurveyInstrument Gyro_ISCWSA_Ex1
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument Gyro_ISCWSA_Ex1
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("974ec53d-2a67-4461-a425-4456ead34af4") },
                    Name = "Gyro_ISCWSA_Ex1",
                    Description = "Default Gyro_ISCWSA_Ex1 survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.Gyro_ISCWSA,
                    UseRelDepthError = false,
                    UseMisalignment = false,
                    UseTrueInclination = false,
                    UseReferenceError = false,
                    UseDrillStringMag = false,
                    UseGyroCompassError = false,
                    CantAngle = 0.0 * DEG2RAD,
                    GyroSwitching = 1,
                    GyroNoiseRed = 1.0,
                    GyroMinDist = 9999,
                    ErrorSourceList = [
                        ErrorSourceFactory.Create_AXY_B(magnitude:0.005),
                        ErrorSourceFactory.Create_AXY_SF(magnitude:0.0005),
                        ErrorSourceFactory.Create_AXY_MS(magnitude:0.05 * DEG2RAD),
                        ErrorSourceFactory.Create_AXY_GB(magnitude:0.005),
                        ErrorSourceFactory.Create_GXY_B1(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_B2(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_RN(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G1(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G2(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G3(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G4(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_SF(magnitude:0.001, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_MIS(magnitude:0.05 * DEG2RAD, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM1(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM2(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM3(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM4(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_SAG(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_DRFR(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.001),
                        ErrorSourceFactory.Create_DSTG(magnitude:5.0e-7),
                        ]
                };
            }
        }
        #endregion

        #region SurveyInstrument Gyro_ISCWSA_Ex2
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument Gyro_ISCWSA_Ex2
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("ce9da9b2-c89a-4156-9fad-6c184c391069") },
                    Name = "Gyro_ISCWSA_Ex2",
                    Description = "Default Gyro_ISCWSA_Ex2 survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.Gyro_ISCWSA,
                    UseRelDepthError = false,
                    UseMisalignment = false,
                    UseTrueInclination = false,
                    UseReferenceError = false,
                    UseDrillStringMag = false,
                    UseGyroCompassError = false,
                    GyroRunningSpeed = 0.6,
                    ExtRefInitInc = 0.0,
                    GyroMinDist = 9999,
                    ErrorSourceList = [
                        ErrorSourceFactory.Create_AXY_B(magnitude:0.005),
                        ErrorSourceFactory.Create_AXY_SF(magnitude:0.0005),
                        ErrorSourceFactory.Create_AXY_MS(magnitude:0.05 * DEG2RAD),
                        ErrorSourceFactory.Create_AXY_GB(magnitude:0.005),
                        ErrorSourceFactory.Create_EXT_REF(magnitude:5.0 * DEG2RAD),
                        ErrorSourceFactory.Create_EXT_TIE(magnitude:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_EXT_MIS(magnitude:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GZ_GD(magnitude:1.0 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GZ_RW(magnitude:1.0 * DEG2RAD / Math.Sqrt(3600.0), startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM1(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM2(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM3(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM4(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_SAG(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_DRFR(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.001),
                        ErrorSourceFactory.Create_DSTG(magnitude:5.0e-7),
                        ]
                };
            }
        }
        #endregion

        #region SurveyInstrument Gyro_ISCWSA_Ex3
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument Gyro_ISCWSA_Ex3
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("cfe07772-1a1b-4829-af2d-75feefa7425d") },
                    Name = "Gyro_ISCWSA_Ex3",
                    Description = "Default Gyro_ISCWSA_Ex3 survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.Gyro_ISCWSA,
                    UseRelDepthError = false,
                    UseMisalignment = false,
                    UseTrueInclination = false,
                    UseReferenceError = false,
                    UseDrillStringMag = false,
                    UseGyroCompassError = false,
                    GyroNoiseRed = 0.5,
                    GyroRunningSpeed = 0.6,
                    GyroMinDist = 2500,
                    ErrorSourceList = [
                        ErrorSourceFactory.Create_AXYZ_XYB(magnitude:0.005),
                        ErrorSourceFactory.Create_AXYZ_ZB(magnitude:0.005),
                        ErrorSourceFactory.Create_AXYZ_SF(magnitude:0.0005),
                        ErrorSourceFactory.Create_AXYZ_MIS(magnitude:0.05 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_B1(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:17.0 * DEG2RAD, initInclination:17.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_B2(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:17.0 * DEG2RAD, initInclination:17.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_RN(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:17.0 * DEG2RAD, initInclination:17.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G1(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:17.0 * DEG2RAD, initInclination:17.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G2(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:17.0 * DEG2RAD, initInclination:17.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G3(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:17.0 * DEG2RAD, initInclination:17.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G4(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:17.0 * DEG2RAD, initInclination:17.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_SF(magnitude:0.001, startInclination:0.0 * DEG2RAD, endInclination:17.0 * DEG2RAD, initInclination:17.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_MIS(magnitude:0.05 * DEG2RAD, startInclination:0.0 * DEG2RAD, endInclination:17.0 * DEG2RAD, initInclination:17.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_GD(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:17.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_RW(magnitude:0.5 * DEG2RAD / Math.Sqrt(3600.0), startInclination:17.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM1(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM2(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM3(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM4(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_SAG(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_DRFR(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.001),
                        ErrorSourceFactory.Create_DSTG(magnitude:5.0e-7),
                        ]
                };
            }
        }
        #endregion

        #region SurveyInstrument Gyro_ISCWSA_Ex4
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument Gyro_ISCWSA_Ex4
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("d5893b4e-3771-412d-802b-9cd6fc547040") },
                    Name = "Gyro_ISCWSA_Ex4",
                    Description = "Default Gyro_ISCWSA_Ex4 survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.Gyro_ISCWSA,
                    UseRelDepthError = false,
                    UseMisalignment = false,
                    UseTrueInclination = false,
                    UseReferenceError = false,
                    UseDrillStringMag = false,
                    UseGyroCompassError = false,
                    CantAngle = 17.0 * DEG2RAD,
                    GyroRunningSpeed = 0.6,
                    GyroSwitching = 0,
                    GyroNoiseRed = 1.0,
                    GyroMinDist = 2500,
                    ErrorSourceList = [
                        ErrorSourceFactory.Create_AXY_B(magnitude:0.005),
                        ErrorSourceFactory.Create_AXY_SF(magnitude:0.0005),
                        ErrorSourceFactory.Create_AXY_MS(magnitude:0.05 * DEG2RAD),
                        ErrorSourceFactory.Create_AXY_GB(magnitude:0.005),
                        ErrorSourceFactory.Create_GXY_B1(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:3.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_B2(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:3.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_RN(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:3.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G1(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:3.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G2(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:3.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G3(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:3.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G4(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:3.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_SF(magnitude:0.001, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:3.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_MIS(magnitude:0.05 * DEG2RAD, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:3.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_GD(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:17.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_RW(magnitude:0.5 * DEG2RAD / Math.Sqrt(3600.0), startInclination:17.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GZ_GD(magnitude:1.0 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:17.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GZ_RW(magnitude:1.0 * DEG2RAD / Math.Sqrt(3600.0), startInclination:0.0 * DEG2RAD, endInclination:17.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM1(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM2(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM3(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM4(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_SAG(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_DRFR(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.001),
                        ErrorSourceFactory.Create_DSTG(magnitude:5.0e-7),
                        ]
                };
            }
        }
        #endregion

        #region SurveyInstrument Gyro_ISCWSA_Ex5
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument Gyro_ISCWSA_Ex5
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("f41c3fc6-d724-4fd1-b71f-22b0f9123602") },
                    Name = "Gyro_ISCWSA_Ex5",
                    Description = "Default Gyro_ISCWSA_Ex5 survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.Gyro_ISCWSA,
                    UseRelDepthError = false,
                    UseMisalignment = false,
                    UseTrueInclination = false,
                    UseReferenceError = false,
                    UseDrillStringMag = false,
                    UseGyroCompassError = false,
                    GyroNoiseRed = 1.0,
                    GyroMinDist = 9999,
                    ErrorSourceList = [
                        ErrorSourceFactory.Create_AXYZ_XYB(magnitude:0.005),
                        ErrorSourceFactory.Create_AXYZ_ZB(magnitude:0.005),
                        ErrorSourceFactory.Create_AXYZ_SF(magnitude:0.0005),
                        ErrorSourceFactory.Create_AXYZ_MIS(magnitude:0.05 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYB1(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYB2(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYRN(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYG1(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYG2(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYG3(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYG4(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_ZB(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_ZRN(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_ZG1(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_ZG2(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_SF(magnitude:0.001, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_MIS(magnitude:0.05 * DEG2RAD, startInclination:0.0 * DEG2RAD, endInclination:0.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_GD(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_RW(magnitude:0.5 * DEG2RAD / Math.Sqrt(3600.0), startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM1(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM2(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM3(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM4(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_SAG(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_DRFR(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.001),
                        ErrorSourceFactory.Create_DSTG(magnitude:5.0e-7),
                        ]
                };
            }
        }
        #endregion

        #region SurveyInstrument Gyro_ISCWSA_Ex6
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument Gyro_ISCWSA_Ex6
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("8ecd8d7a-b7de-458a-8e04-c974ea0ea4cb") },
                    Name = "Gyro_ISCWSA_Ex6",
                    Description = "Default Gyro_ISCWSA_Ex6 survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.Gyro_ISCWSA,
                    UseRelDepthError = false,
                    UseMisalignment = false,
                    UseTrueInclination = false,
                    UseReferenceError = false,
                    UseDrillStringMag = false,
                    UseGyroCompassError = false,
                    GyroNoiseRed = 1.0,
                    GyroMinDist = 9999,
                    ErrorSourceList = [
                        ErrorSourceFactory.Create_AXYZ_XYB(magnitude:0.005),
                        ErrorSourceFactory.Create_AXYZ_ZB(magnitude:0.005),
                        ErrorSourceFactory.Create_AXYZ_SF(magnitude:0.0005),
                        ErrorSourceFactory.Create_AXYZ_MIS(magnitude:0.05 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYB1(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYB2(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYRN(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYG1(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYG2(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYG3(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYG4(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_ZB(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_ZRN(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_ZG1(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_ZG2(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_SF(magnitude:0.001, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_MIS(magnitude:0.05 * DEG2RAD, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:-1.0 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM1(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM2(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM3(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM4(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_SAG(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_DRFR(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.5),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.001),
                        ErrorSourceFactory.Create_DSTG(magnitude:5.0e-7),
                        ]
                };
            }
        }
        #endregion

        #region SurveyInstrument SurveyInstrumentAll
        public static OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument SurveyInstrumentAll
        {
            get
            {
                return new()
                {
                    MetaInfo = new MetaInfo() { HttpHostName = "https://app.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("04b11de3-0c9a-4862-937c-ddfdec9c3f96") },
                    Name = "SurveyInstrumentAll",
                    Description = "Default SurveyInstrumentAll survey instrument",
                    CreationDate = DateTimeOffset.UtcNow,
                    LastModificationDate = DateTimeOffset.UtcNow,
                    ModelType = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrumentModelType.Gyro_ISCWSA,
                    UseRelDepthError = false,
                    UseMisalignment = false,
                    UseTrueInclination = false,
                    UseReferenceError = false,
                    UseDrillStringMag = false,
                    UseGyroCompassError = false,
                    GyroNoiseRed = 1.0,
                    GyroMinDist = 9999,
                    ErrorSourceList = [
                        ErrorSourceFactory.Create_DRFR(magnitude:0.35),
                        ErrorSourceFactory.Create_DSFS(magnitude:0.00056),
                        ErrorSourceFactory.Create_DSTG(magnitude:2.5e-7),
                        ErrorSourceFactory.Create_ABXY_TI1S(magnitude:0.004),
                        ErrorSourceFactory.Create_ABXY_TI2S(magnitude:0.004),
                        ErrorSourceFactory.Create_ABZ(magnitude:0.004),
                        ErrorSourceFactory.Create_ASXY_TI1S(magnitude:0.0005),
                        ErrorSourceFactory.Create_ASXY_TI2S(magnitude:0.0005),
                        ErrorSourceFactory.Create_ASXY_TI3S(magnitude:0.0005),
                        ErrorSourceFactory.Create_ASZ(magnitude:0.0005),
                        ErrorSourceFactory.Create_MBXY_TI1(magnitude:70e-9),
                        ErrorSourceFactory.Create_MBXY_TI2(magnitude:70e-9),
                        ErrorSourceFactory.Create_MBZ(magnitude:70e-9),
                        ErrorSourceFactory.Create_MSXY_TI1(magnitude:0.0016),
                        ErrorSourceFactory.Create_MSXY_TI2(magnitude:0.0016),
                        ErrorSourceFactory.Create_MSXY_TI3(magnitude:0.0016),
                        ErrorSourceFactory.Create_MSZ(magnitude:0.0016),
                        ErrorSourceFactory.Create_DEC_U(magnitude:0.16 * DEG2RAD),
                        ErrorSourceFactory.Create_DEC_OS(magnitude:0.24 * DEG2RAD),
                        ErrorSourceFactory.Create_DEC_OH(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_DEC_OI(magnitude:0.05 * DEG2RAD),
                        ErrorSourceFactory.Create_DECR(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_DBH_U(magnitude:2350e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_DBH_OS(magnitude:3359e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_DBH_OH(magnitude:2840e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_DBH_OI(magnitude:356e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_DBHR(magnitude:3000e-9 * DEG2RAD),
                        ErrorSourceFactory.Create_AMID(magnitude:220e-6),
                        ErrorSourceFactory.Create_SAGE(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM1(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM2(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM3E(magnitude:0.3 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM4E(magnitude:0.3 * DEG2RAD),
                        ErrorSourceFactory.Create_XCLH(magnitude:0.167),
                        ErrorSourceFactory.Create_XCLL(magnitude:0.167),
                        ErrorSourceFactory.Create_AXY_B(magnitude:0.005),
                        ErrorSourceFactory.Create_AXY_SF(magnitude:0.0005),
                        ErrorSourceFactory.Create_AXY_MS(magnitude:0.05 * DEG2RAD),
                        ErrorSourceFactory.Create_AXY_GB(magnitude:0.005),
                        ErrorSourceFactory.Create_GXY_B1(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_B2(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_RN(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G1(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G2(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G3(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_G4(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_SF(magnitude:0.001, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXY_MIS(magnitude:0.05 * DEG2RAD, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM3(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_XYM4(magnitude:0.2 * DEG2RAD),
                        ErrorSourceFactory.Create_SAG(magnitude:0.1 * DEG2RAD),
                        ErrorSourceFactory.Create_DRFS(magnitude:0.5),
                        ErrorSourceFactory.Create_AXYZ_XYB(magnitude:0.005),
                        ErrorSourceFactory.Create_AXYZ_ZB(magnitude:0.005),
                        ErrorSourceFactory.Create_AXYZ_SF(magnitude:0.0005),
                        ErrorSourceFactory.Create_AXYZ_MIS(magnitude:0.05 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYB1(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYB2(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYRN(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYG1(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYG2(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYG3(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_XYG4(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_ZB(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_ZRN(magnitude:0.1 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_ZG1(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_ZG2(magnitude:0.5 * DEG2RAD / 3600.0, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_SF(magnitude:0.001, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_MIS(magnitude:0.05 * DEG2RAD, startInclination:0.0 * DEG2RAD, endInclination:150.0 * DEG2RAD, initInclination:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_EXT_REF(magnitude:5.0 * DEG2RAD),
                        ErrorSourceFactory.Create_EXT_TIE(magnitude:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_EXT_MIS(magnitude:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_ABIXY_TI1S(magnitude:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_ABIXY_TI2S(magnitude:0.0 * DEG2RAD),
                        ErrorSourceFactory.Create_GXYZ_GD(magnitude:0.5 * DEG2RAD / 3600.0),
                        ErrorSourceFactory.Create_GXYZ_RW(magnitude:0.5 * DEG2RAD / Math.Sqrt(3600.0)),
                        ErrorSourceFactory.Create_GXY_GD(magnitude:0.5 * DEG2RAD / 3600.0),
                        ErrorSourceFactory.Create_GXY_RW(magnitude:0.5 * DEG2RAD / Math.Sqrt(3600.0)),
                        ErrorSourceFactory.Create_GZ_GD(magnitude:1.0 * DEG2RAD / 3600.0),
                        ErrorSourceFactory.Create_GZ_RW(magnitude:1.0 * DEG2RAD / Math.Sqrt(3600.0)),
                        ]
                };
            }
        }
        #endregion
        #endregion
    }
}