using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OSDC.DotnetLibraries.General.DataManagement;
using NORCE.Drilling.SurveyInstrument.Service.Managers;
using OSDC.DotnetLibraries.Drilling.Surveying;
using NORCE.Drilling.SurveyInstrument.Model;

namespace NORCE.Drilling.SurveyInstrument.Service.Controllers
{
    [Produces("application/json")]
    [Route("[controller]")]
    [ApiController]
    public class ErrorSourceController : ControllerBase
    {
        private readonly ILogger<ErrorSourceManager> _logger;
        private readonly ErrorSourceManager _errorSourceManager;

        public ErrorSourceController(ILogger<ErrorSourceManager> logger, SqlConnectionManager connectionManager)
        {
            _logger = logger;
            _errorSourceManager = ErrorSourceManager.GetInstance(_logger, connectionManager);
        }

        /// <summary>
        /// Returns the list of Guid of all ErrorSource present in the microservice database at endpoint SurveyInstrument/api/ErrorSource
        /// </summary>
        /// <returns>the list of Guid of all ErrorSource present in the microservice database at endpoint SurveyInstrument/api/ErrorSource</returns>
        [HttpGet(Name = "GetAllErrorSourceId")]
        public ActionResult<IEnumerable<Guid>> GetAllErrorSourceId()
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementGetAllErrorSourceIdPerDay();
            var ids = _errorSourceManager.GetAllErrorSourceId();
            if (ids != null)
            {
                return Ok(ids);
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Returns the list of MetaInfo of all ErrorSource present in the microservice database, at endpoint SurveyInstrument/api/ErrorSource/MetaInfo
        /// </summary>
        /// <returns>the list of MetaInfo of all ErrorSource present in the microservice database, at endpoint SurveyInstrument/api/ErrorSource/MetaInfo</returns>
        [HttpGet("MetaInfo", Name = "GetAllErrorSourceMetaInfo")]
        public ActionResult<IEnumerable<MetaInfo>> GetAllErrorSourceMetaInfo()
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementGetAllErrorSourceMetaInfoPerDay();
            var vals = _errorSourceManager.GetAllErrorSourceMetaInfo();
            if (vals != null)
            {
                return Ok(vals);
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Returns the ErrorSource identified by its Guid from the microservice database, at endpoint SurveyInstrument/api/ErrorSource/MetaInfo/id
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>the ErrorSource identified by its Guid from the microservice database, at endpoint SurveyInstrument/api/ErrorSource/MetaInfo/id</returns>
        [HttpGet("{id}", Name = "GetErrorSourceById")]
        public ActionResult<ErrorSource?> GetErrorSourceById(Guid id)
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementGetErrorSourceByIdPerDay();
            if (!id.Equals(Guid.Empty))
            {
                var val = _errorSourceManager.GetErrorSourceById(id);
                if (val != null)
                {
                    return Ok(val);
                }
                else
                {
                    return NotFound();
                }
            }
            else
            {
                return BadRequest();
            }
        }

        /// <summary>
        /// Returns the list of all ErrorSource present in the microservice database, at endpoint SurveyInstrument/api/ErrorSource/HeavyData
        /// </summary>
        /// <returns>the list of all ErrorSource present in the microservice database, at endpoint SurveyInstrument/api/ErrorSource/HeavyData</returns>
        [HttpGet("HeavyData", Name = "GetAllErrorSource")]
        public ActionResult<IEnumerable<ErrorSource?>> GetAllErrorSource()
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementGetAllErrorSourcePerDay();
            var vals = _errorSourceManager.GetAllErrorSource();
            if (vals != null)
            {
                return Ok(vals);
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Performs calculation on the given ErrorSource and adds it to the microservice database, at the endpoint SurveyInstrument/api/ErrorSource
        /// </summary>
        /// <param name="errorSource"></param>
        /// <returns>true if the given ErrorSource has been added successfully to the microservice database, at the endpoint SurveyInstrument/api/ErrorSource</returns>
        [HttpPost(Name = "PostErrorSource")]
        public ActionResult PostErrorSource([FromBody] ErrorSource? data)
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementPostErrorSourcePerDay();
            // Check if errorSource exists in the database through ID
            if (data != null && data.MetaInfo != null && data.MetaInfo.ID != Guid.Empty)
            {
                var existingData = _errorSourceManager.GetErrorSourceById(data.MetaInfo.ID);
                if (existingData == null)
                {   
                    //  If errorSource was not found, call AddErrorSource
                    if (_errorSourceManager.AddErrorSource(data))
                    {
                        return Ok(); // status=OK is used rather than status=Created because NSwag auto-generated controllers use 200 (OK) rather than 201 (Created) as return codes
                    }
                    else
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError);
                    }
                }
                else
                {
                    _logger.LogWarning("The given ErrorSource already exists and will not be added");
                    return StatusCode(StatusCodes.Status409Conflict);
                }
            }
            else
            {
                _logger.LogWarning("The given ErrorSource is null, badly formed, or its ID is empty");
                return BadRequest();
            }
        }

        /// <summary>
        /// Performs calculation on the given ErrorSource and updates it in the microservice database, at the endpoint SurveyInstrument/api/ErrorSource/id
        /// </summary>
        /// <param name="errorSource"></param>
        /// <returns>true if the given ErrorSource has been updated successfully to the microservice database, at the endpoint SurveyInstrument/api/ErrorSource/id</returns>
        [HttpPut("{id}", Name = "PutErrorSourceById")]
        public ActionResult PutErrorSourceById(Guid id, [FromBody] ErrorSource? data)
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementPutErrorSourceByIdPerDay();
            // Check if ErrorSource is in the data base
            if (data != null && data.MetaInfo != null && data.MetaInfo.ID.Equals(id))
            {
                var existingData = _errorSourceManager.GetErrorSourceById(id);
                if (existingData != null)
                {
                    if (_errorSourceManager.UpdateErrorSourceById(id, data))
                    {
                        return Ok();
                    }
                    else
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError);
                    }
                }
                else
                {
                    _logger.LogWarning("The given ErrorSource has not been found in the database");
                    return NotFound();
                }
            }
            else
            {
                _logger.LogWarning("The given ErrorSource is null, badly formed, or its does not match the ID to update");
                return BadRequest();
            }
        }

        /// <summary>
        /// Deletes the ErrorSource of given ID from the microservice database, at the endpoint SurveyInstrument/api/ErrorSource/id
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>true if the ErrorSource was deleted from the microservice database, at the endpoint SurveyInstrument/api/ErrorSource/id</returns>
        [HttpDelete("{id}", Name = "DeleteErrorSourceById")]
        public ActionResult DeleteErrorSourceById(Guid id)
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementDeleteErrorSourceByIdPerDay();
            if (_errorSourceManager.GetErrorSourceById(id) != null)
            {
                if (_errorSourceManager.DeleteErrorSourceById(id))
                {
                    return Ok();
                }
                else
                {
                    return StatusCode(StatusCodes.Status500InternalServerError);
                }
            }
            else
            {
                _logger.LogWarning("The ErrorSource of given ID does not exist");
                return NotFound();
            }
        }
    }
}
