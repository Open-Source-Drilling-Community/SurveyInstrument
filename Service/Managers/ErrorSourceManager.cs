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
    public class ErrorSourceManager
    {
        private static ErrorSourceManager? _instance = null;
        private readonly ILogger<ErrorSourceManager> _logger;
        private readonly SqlConnectionManager _connectionManager;

        private ErrorSourceManager(ILogger<ErrorSourceManager> logger, SqlConnectionManager connectionManager)
        {
            _logger = logger;
            _connectionManager = connectionManager;

            // make sure database contains default ErrorSources
            List<Guid>? ids = GetAllErrorSourceId();
            if (ids != null && ids.Count < 1)
            {
                FillDefault();
            }
            ids = GetAllErrorSourceId();
            if (ids != null)
            {
                bool isCorrupted = false;
                try
                {
                    foreach (Guid id in ids)
                    {
                        ErrorSource? si = GetErrorSourceById(id);
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
                    _logger.LogWarning("The ErrorSourceTable is corrupted: clearing it and filling it with default ErrorSources");
                    Clear();
                    FillDefault();
                }
            }
        }

        public static ErrorSourceManager GetInstance(ILogger<ErrorSourceManager> logger, SqlConnectionManager connectionManager)
        {
            _instance ??= new ErrorSourceManager(logger, connectionManager);
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
                    command.CommandText = "SELECT COUNT(*) FROM ErrorSourceTable";
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
                        _logger.LogError(ex, "Impossible to count records in the ErrorSourceTable");
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
                    //empty ErrorSourceTable
                    var command = connection.CreateCommand();
                    command.CommandText = "DELETE FROM ErrorSourceTable";
                    command.ExecuteNonQuery();

                    transaction.Commit();
                    success = true;
                }
                catch (SqliteException ex)
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "Impossible to clear the ErrorSourceTable");
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
                command.CommandText = $"SELECT COUNT(*) FROM ErrorSourceTable WHERE ID = '{guid}'";
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
                    _logger.LogError(ex, "Impossible to count rows from ErrorSourceTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return count >= 1;
        }

        /// <summary>
        /// Returns the list of Guid of all ErrorSource present in the microservice database 
        /// </summary>
        /// <returns>the list of Guid of all ErrorSource present in the microservice database</returns>
        public List<Guid>? GetAllErrorSourceId()
        {
            List<Guid> ids = [];
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT ID FROM ErrorSourceTable";
                try
                {
                    using var reader = command.ExecuteReader();
                    while (reader.Read() && !reader.IsDBNull(0))
                    {
                        Guid id = reader.GetGuid(0);
                        ids.Add(id);
                    }
                    _logger.LogInformation("Returning the list of ID of existing records from ErrorSourceTable");
                    return ids;
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Impossible to get IDs from ErrorSourceTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return null;
        }

        /// <summary>
        /// Returns the list of MetaInfo of all ErrorSource present in the microservice database 
        /// </summary>
        /// <returns>the list of MetaInfo of all ErrorSource present in the microservice database</returns>
        public List<MetaInfo?>? GetAllErrorSourceMetaInfo()
        {
            List<MetaInfo?> metaInfos = new();
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT MetaInfo FROM ErrorSourceTable";
                try
                {
                    using var reader = command.ExecuteReader();
                    while (reader.Read() && !reader.IsDBNull(0))
                    {
                        string mInfo = reader.GetString(0);
                        MetaInfo? metaInfo = JsonSerializer.Deserialize<MetaInfo>(mInfo, JsonSettings.Options);
                        metaInfos.Add(metaInfo);
                    }
                    _logger.LogInformation("Returning the list of MetaInfo of existing records from ErrorSourceTable");
                    return metaInfos;
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Impossible to get IDs from ErrorSourceTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return null;
        }

        /// <summary>
        /// Returns the ErrorSource identified by its Guid from the microservice database 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>the ErrorSource identified by its Guid from the microservice database</returns>
        public ErrorSource? GetErrorSourceById(Guid guid)
        {
            if (!guid.Equals(Guid.Empty))
            {
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    ErrorSource? errorSource;
                    var command = connection.CreateCommand();
                    command.CommandText = $"SELECT ErrorSource FROM ErrorSourceTable WHERE ID = '{guid}'";
                    try
                    {
                        using var reader = command.ExecuteReader();
                        if (reader.Read() && !reader.IsDBNull(0))
                        {
                            string data = reader.GetString(0);
                            errorSource = JsonSerializer.Deserialize<ErrorSource>(data, JsonSettings.Options);
                            if (errorSource != null && errorSource.MetaInfo != null && !errorSource.MetaInfo.ID.Equals(guid))
                                throw new SqliteException("SQLite database corrupted: returned ErrorSource is null or has been jsonified with the wrong ID.", 1);
                        }
                        else
                        {
                            _logger.LogInformation("No ErrorSource of given ID in the database");
                            return null;
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to get the ErrorSource with the given ID from ErrorSourceTable");
                        return null;
                    }
                    _logger.LogInformation("Returning the ErrorSource of given ID from ErrorSourceTable");
                    return errorSource;
                }
                else
                {
                    _logger.LogWarning("Impossible to access the SQLite database");
                }
            }
            else
            {
                _logger.LogWarning("The given ErrorSource ID is null or empty");
            }
            return null;
        }

        /// <summary>
        /// Returns the list of all ErrorSource present in the microservice database 
        /// </summary>
        /// <returns>the list of all ErrorSource present in the microservice database</returns>
        public List<ErrorSource?>? GetAllErrorSource()
        {
            List<ErrorSource?> vals = [];
            var connection = _connectionManager.GetConnection();
            if (connection != null)
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT ErrorSource FROM ErrorSourceTable";
                try
                {
                    using var reader = command.ExecuteReader();
                    while (reader.Read() && !reader.IsDBNull(0))
                    {
                        string data = reader.GetString(0);
                        ErrorSource? errorSource = JsonSerializer.Deserialize<ErrorSource>(data, JsonSettings.Options);
                        vals.Add(errorSource);
                    }
                    _logger.LogInformation("Returning the list of existing ErrorSource from ErrorSourceTable");
                    return vals;
                }
                catch (SqliteException ex)
                {
                    _logger.LogError(ex, "Impossible to get ErrorSource from ErrorSourceTable");
                }
            }
            else
            {
                _logger.LogWarning("Impossible to access the SQLite database");
            }
            return null;
        }

        /// <summary>
        /// Add the given ErrorSource to the microservice database
        /// </summary>
        /// <param name="errorSource"></param>
        /// <returns>true if the given ErrorSource has been added successfully to the microservice database</returns>
        public bool AddErrorSource(ErrorSource? errorSource)
        {
            if (errorSource != null && errorSource.MetaInfo != null && errorSource.MetaInfo.ID != Guid.Empty)
            {
                //update ErrorSourceTable
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    using SqliteTransaction transaction = connection.BeginTransaction();
                    bool success = true;
                    try
                    {
                        //add the ErrorSource to the ErrorSourceTable
                        string metaInfo = JsonSerializer.Serialize(errorSource.MetaInfo, JsonSettings.Options);
                        string data = JsonSerializer.Serialize(errorSource, JsonSettings.Options);
                        var command = connection.CreateCommand();
                        command.CommandText = "INSERT INTO ErrorSourceTable (" +
                            "ID, " +
                            "MetaInfo, " +
                            "ErrorSource" +
                            ") VALUES (" +
                            $"'{errorSource.MetaInfo.ID}', " +
                            $"'{metaInfo}', " +
                            $"'{data}'" +
                            ")";
                        int count = command.ExecuteNonQuery();
                        if (count != 1)
                        {
                            _logger.LogWarning("Impossible to insert the given ErrorSource into the ErrorSourceTable");
                            success = false;
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to add the given ErrorSource into ErrorSourceTable");
                        success = false;
                    }
                    //finalizing SQL transaction
                    if (success)
                    {
                        transaction.Commit();
                        _logger.LogInformation("Added the given ErrorSource of given ID into the ErrorSourceTable successfully");
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
                _logger.LogWarning("The ErrorSource ID or the ID of its input are null or empty");
            }
            return false;
        }

        /// <summary>
        /// Performs calculation on the given ErrorSource and updates it in the microservice database
        /// </summary>
        /// <param name="errorSource"></param>
        /// <returns>true if the given ErrorSource has been updated successfully</returns>
        public bool UpdateErrorSourceById(Guid guid, ErrorSource? errorSource)
        {
            bool success = true;
            if (guid != Guid.Empty && errorSource != null && errorSource.MetaInfo != null && errorSource.MetaInfo.ID == guid)
            {
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    using SqliteTransaction transaction = connection.BeginTransaction();
                    //update fields in ErrorSourceTable
                    try
                    {
                        string metaInfo = JsonSerializer.Serialize(errorSource.MetaInfo, JsonSettings.Options);
                        string data = JsonSerializer.Serialize(errorSource, JsonSettings.Options);
                        var command = connection.CreateCommand();
                        command.CommandText = $"UPDATE ErrorSourceTable SET " +
                            $"MetaInfo = '{metaInfo}', " +
                            $"ErrorSource = '{data}' " +
                            $"WHERE ID = '{guid}'";
                        int count = command.ExecuteNonQuery();
                        if (count != 1)
                        {
                            _logger.LogWarning("Impossible to update the ErrorSource");
                            success = false;
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to update the ErrorSource");
                        success = false;
                    }

                    // Finalizing
                    if (success)
                    {
                        transaction.Commit();
                        _logger.LogInformation("Updated the given ErrorSource successfully");
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
                _logger.LogWarning("The ErrorSource ID or the ID of some of its attributes are null or empty");
            }
            return false;
        }

        /// <summary>
        /// Deletes the ErrorSource of given ID from the microservice database
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>true if the ErrorSource was deleted from the microservice database</returns>
        public bool DeleteErrorSourceById(Guid guid)
        {
            if (!guid.Equals(Guid.Empty))
            {
                var connection = _connectionManager.GetConnection();
                if (connection != null)
                {
                    using var transaction = connection.BeginTransaction();
                    bool success = true;
                    //delete ErrorSource from ErrorSourceTable
                    try
                    {
                        var command = connection.CreateCommand();
                        command.CommandText = $"DELETE FROM ErrorSourceTable WHERE ID = '{guid}'";
                        int count = command.ExecuteNonQuery();
                        if (count < 0)
                        {
                            _logger.LogWarning("Impossible to delete the ErrorSource of given ID from the ErrorSourceTable");
                            success = false;
                        }
                    }
                    catch (SqliteException ex)
                    {
                        _logger.LogError(ex, "Impossible to delete the ErrorSource of given ID from ErrorSourceTable");
                        success = false;
                    }
                    if (success)
                    {
                        transaction.Commit();
                        _logger.LogInformation("Removed the ErrorSource of given ID from the ErrorSourceTable successfully");
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
                _logger.LogWarning("The ErrorSource ID is null or empty");
            }
            return false;
        }

        /// <summary>
        /// populate database with default error sources (with settable member variables set to default)
        /// </summary>
        private void FillDefault()
        {
            List<ErrorSource?> errorSourceList = [
                ErrorSourceFactory.Create_XYM1(),
                ErrorSourceFactory.Create_XYM2(),
                ErrorSourceFactory.Create_XYM3(),
                ErrorSourceFactory.Create_XYM4(),
                ErrorSourceFactory.Create_SAG(),
                ErrorSourceFactory.Create_DRFR(),
                ErrorSourceFactory.Create_DRFS(),
                ErrorSourceFactory.Create_DSFS(),
                ErrorSourceFactory.Create_DSTG(),
                ErrorSourceFactory.Create_XYM3E(),
                ErrorSourceFactory.Create_XYM4E(),
                ErrorSourceFactory.Create_SAGE(),
                ErrorSourceFactory.Create_XCLH(),
                ErrorSourceFactory.Create_XCLL(),
                ErrorSourceFactory.Create_XCLA(),
                ErrorSourceFactory.Create_ABXY_TI1S(),
                ErrorSourceFactory.Create_ABXY_TI2S(),
                ErrorSourceFactory.Create_ABIXY_TI1S(),
                ErrorSourceFactory.Create_ABIXY_TI2S(),
                ErrorSourceFactory.Create_ABZ(),
                ErrorSourceFactory.Create_ASXY_TI1S(),
                ErrorSourceFactory.Create_ASXY_TI2S(),
                ErrorSourceFactory.Create_ASXY_TI3S(),
                ErrorSourceFactory.Create_ASZ(),
                ErrorSourceFactory.Create_MBXY_TI1(),
                ErrorSourceFactory.Create_MBXY_TI2(),
                ErrorSourceFactory.Create_MBZ(),
                ErrorSourceFactory.Create_MSXY_TI1(),
                ErrorSourceFactory.Create_MSXY_TI2(),
                ErrorSourceFactory.Create_MSXY_TI3(),
                ErrorSourceFactory.Create_MSZ(),
                ErrorSourceFactory.Create_AMID(),
                ErrorSourceFactory.Create_DEC_U(),
                ErrorSourceFactory.Create_DEC_OS(),
                ErrorSourceFactory.Create_DEC_OH(),
                ErrorSourceFactory.Create_DEC_OI(),
                ErrorSourceFactory.Create_DECR(),
                ErrorSourceFactory.Create_DBH_U(),
                ErrorSourceFactory.Create_DBH_OS(),
                ErrorSourceFactory.Create_DBH_OH(),
                ErrorSourceFactory.Create_DBH_OI(),
                ErrorSourceFactory.Create_DBHR(),
                ErrorSourceFactory.Create_AXYZ_XYB(),
                ErrorSourceFactory.Create_AXYZ_ZB(),
                ErrorSourceFactory.Create_AXYZ_SF(),
                ErrorSourceFactory.Create_AXYZ_MIS(),
                ErrorSourceFactory.Create_AXY_B(),
                ErrorSourceFactory.Create_AXY_SF(),
                ErrorSourceFactory.Create_AXY_MS(),
                ErrorSourceFactory.Create_AXY_GB(),
                ErrorSourceFactory.Create_GXYZ_XYB1(),
                ErrorSourceFactory.Create_GXYZ_XYB2(),
                ErrorSourceFactory.Create_GXYZ_XYRN(),
                ErrorSourceFactory.Create_GXYZ_XYG1(),
                ErrorSourceFactory.Create_GXYZ_XYG2(),
                ErrorSourceFactory.Create_GXYZ_XYG3(),
                ErrorSourceFactory.Create_GXYZ_XYG4(),
                ErrorSourceFactory.Create_GXYZ_ZB(),
                ErrorSourceFactory.Create_GXYZ_ZRN(),
                ErrorSourceFactory.Create_GXYZ_ZG1(),
                ErrorSourceFactory.Create_GXYZ_ZG2(),
                ErrorSourceFactory.Create_GXYZ_SF(),
                ErrorSourceFactory.Create_GXYZ_MIS(),
                ErrorSourceFactory.Create_GXY_B1(),
                ErrorSourceFactory.Create_GXY_B2(),
                ErrorSourceFactory.Create_GXY_RN(),
                ErrorSourceFactory.Create_GXY_G1(),
                ErrorSourceFactory.Create_GXY_G2(),
                ErrorSourceFactory.Create_GXY_G3(),
                ErrorSourceFactory.Create_GXY_G4(),
                ErrorSourceFactory.Create_GXY_SF(),
                ErrorSourceFactory.Create_GXY_MIS(),
                ErrorSourceFactory.Create_EXT_REF(),
                ErrorSourceFactory.Create_EXT_TIE(),
                ErrorSourceFactory.Create_EXT_MIS(),
                ErrorSourceFactory.Create_GXYZ_GD(),
                ErrorSourceFactory.Create_GXYZ_RW(),
                ErrorSourceFactory.Create_GXY_GD(),
                ErrorSourceFactory.Create_GXY_RW(),
                ErrorSourceFactory.Create_GZ_GD(),
                ErrorSourceFactory.Create_GZ_RW(),
            ];
            foreach (ErrorSource? es in errorSourceList)
            {
                AddErrorSource(es);
            }
        }
    }
}