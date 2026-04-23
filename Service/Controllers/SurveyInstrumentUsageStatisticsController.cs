using Microsoft.AspNetCore.Mvc;
using NORCE.Drilling.SurveyInstrument.Model;

namespace NORCE.Drilling.SurveyInstrument.Service.Controllers
{
    [Produces("application/json")]
    [Route("[controller]")]
    [ApiController]
    public class SurveyInstrumentUsageStatisticsController : ControllerBase
    {
        /// <summary>
        /// Returns the usage statistics present in the microservice database at endpoint SurveyInstrument/api/SurveyInstrumentUsageStatistics
        /// </summary>
        /// <returns></returns>
        [HttpGet(Name = "GetSurveyInstrumentUsageStatistics")]
        public ActionResult<UsageStatisticsSurveyInstrument> GetSurveyInstrumentUsageStatistics()
        {
            return Ok(UsageStatisticsSurveyInstrument.Instance);
        }
    }
}
