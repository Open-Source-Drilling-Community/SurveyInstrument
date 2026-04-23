using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OSDC.DotnetLibraries.General.DataManagement;
using NORCE.Drilling.SurveyInstrument.Service.Managers;
using NORCE.Drilling.SurveyInstrument.Model;

namespace NORCE.Drilling.SurveyInstrument.Service.Controllers
{
    [Produces("application/json")]
    [Route("[controller]")]
    [ApiController]
    public class SurveyInstrumentController : ControllerBase
    {
        private readonly ILogger<SurveyInstrumentManager> _logger;
        private readonly SurveyInstrumentManager _surveyInstrumentManager;

        public SurveyInstrumentController(ILogger<SurveyInstrumentManager> logger, ILogger<ErrorSourceManager> errorSourceLogger, SqlConnectionManager connectionManager)
        {
            _logger = logger;
            _surveyInstrumentManager = SurveyInstrumentManager.GetInstance(_logger, errorSourceLogger, connectionManager);
        }

        /// <summary>
        /// Returns the list of Guid of all SurveyInstrument present in the microservice database at endpoint SurveyInstrument/api/SurveyInstrument
        /// </summary>
        /// <returns>the list of Guid of all SurveyInstrument present in the microservice database at endpoint SurveyInstrument/api/SurveyInstrument</returns>
        [HttpGet(Name = "GetAllSurveyInstrumentId")]
        public ActionResult<IEnumerable<Guid>> GetAllSurveyInstrumentId()
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementGetAllSurveyInstrumentIdPerDay();
            var ids = _surveyInstrumentManager.GetAllSurveyInstrumentId();
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
        /// Returns the list of MetaInfo of all SurveyInstrument present in the microservice database, at endpoint SurveyInstrument/api/SurveyInstrument/MetaInfo
        /// </summary>
        /// <returns>the list of MetaInfo of all SurveyInstrument present in the microservice database, at endpoint SurveyInstrument/api/SurveyInstrument/MetaInfo</returns>
        [HttpGet("MetaInfo", Name = "GetAllSurveyInstrumentMetaInfo")]
        public ActionResult<IEnumerable<MetaInfo>> GetAllSurveyInstrumentMetaInfo()
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementGetAllSurveyInstrumentMetaInfoPerDay();
            var vals = _surveyInstrumentManager.GetAllSurveyInstrumentMetaInfo();
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
        /// Returns the SurveyInstrument identified by its Guid from the microservice database, at endpoint SurveyInstrument/api/SurveyInstrument/MetaInfo/id
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>the SurveyInstrument identified by its Guid from the microservice database, at endpoint SurveyInstrument/api/SurveyInstrument/MetaInfo/id</returns>
        [HttpGet("{id}", Name = "GetSurveyInstrumentById")]
        public ActionResult<OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument?> GetSurveyInstrumentById(Guid id)
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementGetSurveyInstrumentByIdPerDay();
            if (!id.Equals(Guid.Empty))
            {
                var val = _surveyInstrumentManager.GetSurveyInstrumentById(id);
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
        /// Returns the list of all SurveyInstrumentLight present in the microservice database, at endpoint SurveyInstrument/api/SurveyInstrument/LightData
        /// </summary>
        /// <returns>the list of all SurveyInstrumentLight present in the microservice database, at endpoint SurveyInstrument/api/SurveyInstrument/LightData</returns>
        [HttpGet("LightData", Name = "GetAllSurveyInstrumentLight")]
        public ActionResult<IEnumerable<SurveyInstrumentLight>> GetAllSurveyInstrumentLight()
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementGetAllSurveyInstrumentLightPerDay();
            var vals = _surveyInstrumentManager.GetAllSurveyInstrumentLight();
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
        /// Returns the list of all SurveyInstrument present in the microservice database, at endpoint SurveyInstrument/api/SurveyInstrument/HeavyData
        /// </summary>
        /// <returns>the list of all SurveyInstrument present in the microservice database, at endpoint SurveyInstrument/api/SurveyInstrument/HeavyData</returns>
        [HttpGet("HeavyData", Name = "GetAllSurveyInstrument")]
        public ActionResult<IEnumerable<OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument?>> GetAllSurveyInstrument()
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementGetAllSurveyInstrumentPerDay();
            var vals = _surveyInstrumentManager.GetAllSurveyInstrument();
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
        /// Performs calculation on the given SurveyInstrument and adds it to the microservice database, at the endpoint SurveyInstrument/api/SurveyInstrument
        /// </summary>
        /// <param name="surveyInstrument"></param>
        /// <returns>true if the given SurveyInstrument has been added successfully to the microservice database, at the endpoint SurveyInstrument/api/SurveyInstrument</returns>
        [HttpPost(Name = "PostSurveyInstrument")]
        public ActionResult PostSurveyInstrument([FromBody] OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument? data)
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementPostSurveyInstrumentPerDay();
            // Check if surveyInstrument exists in the database through ID
            if (data != null && data.MetaInfo != null && data.MetaInfo.ID != Guid.Empty)
            {
                var existingData = _surveyInstrumentManager.GetSurveyInstrumentById(data.MetaInfo.ID);
                if (existingData == null)
                {   
                    //  If surveyInstrument was not found, call AddSurveyInstrument, where the surveyInstrument.Calculate()
                    // method is called. 
                    if (_surveyInstrumentManager.AddSurveyInstrument(data))
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
                    _logger.LogWarning("The given SurveyInstrument already exists and will not be added");
                    return StatusCode(StatusCodes.Status409Conflict);
                }
            }
            else
            {
                _logger.LogWarning("The given SurveyInstrument is null, badly formed, or its ID is empty");
                return BadRequest();
            }
        }

        /// <summary>
        /// Performs calculation on the given SurveyInstrument and updates it in the microservice database, at the endpoint SurveyInstrument/api/SurveyInstrument/id
        /// </summary>
        /// <param name="surveyInstrument"></param>
        /// <returns>true if the given SurveyInstrument has been updated successfully to the microservice database, at the endpoint SurveyInstrument/api/SurveyInstrument/id</returns>
        [HttpPut("{id}", Name = "PutSurveyInstrumentById")]
        public ActionResult PutSurveyInstrumentById(Guid id, [FromBody] OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument? data)
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementPutSurveyInstrumentByIdPerDay();
            // Check if SurveyInstrument is in the data base
            if (data != null && data.MetaInfo != null && data.MetaInfo.ID.Equals(id))
            {
                var existingData = _surveyInstrumentManager.GetSurveyInstrumentById(id);
                if (existingData != null)
                {
                    if (_surveyInstrumentManager.UpdateSurveyInstrumentById(id, data))
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
                    _logger.LogWarning("The given SurveyInstrument has not been found in the database");
                    return NotFound();
                }
            }
            else
            {
                _logger.LogWarning("The given SurveyInstrument is null, badly formed, or its does not match the ID to update");
                return BadRequest();
            }
        }

        /// <summary>
        /// Deletes the SurveyInstrument of given ID from the microservice database, at the endpoint SurveyInstrument/api/SurveyInstrument/id
        /// </summary>
        /// <param name="guid"></param>
        /// <returns>true if the SurveyInstrument was deleted from the microservice database, at the endpoint SurveyInstrument/api/SurveyInstrument/id</returns>
        [HttpDelete("{id}", Name = "DeleteSurveyInstrumentById")]
        public ActionResult DeleteSurveyInstrumentById(Guid id)
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementDeleteSurveyInstrumentByIdPerDay();
            if (_surveyInstrumentManager.GetSurveyInstrumentById(id) != null)
            {
                if (_surveyInstrumentManager.DeleteSurveyInstrumentById(id))
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
                _logger.LogWarning("The SurveyInstrument of given ID does not exist");
                return NotFound();
            }
        }
    }
}
