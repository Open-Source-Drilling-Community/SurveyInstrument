using OSDC.Drilling.SurveyInstrument.Service;
using Microsoft.AspNetCore.Mvc;
using OSDC.Drilling.SurveyInstrument.Model;
using OSDC.Drilling.SurveyInstrument.Service.Managers;
using OSDC.DotnetLibraries.General.DataManagement;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OSDC.Drilling.SurveyInstrument.Service.Controllers;

[Produces("application/json"), Route("[controller]"), ApiController]
public class SurveyInstrumentIdentityController : ControllerBase
{
        private readonly SqlConnectionManager _discoveryConnections;

        /// <summary>Discover SurveyInstrumentIdentity resources as bounded light records; retrieve selected full records by ID.</summary>
        [HttpGet("LightData", Name = "DiscoverSurveyInstrumentIdentity")]
        public ActionResult<OSDC.Drilling.SurveyInstrument.Model.SurveyInstrumentIdentityDiscoveryPage> DiscoverSurveyInstrumentIdentity([FromQuery] int offset = 0, [FromQuery] int limit = 50, [FromQuery] string? name = null)
        {
            UsageStatisticsSurveyInstrument.Instance.IncrementDiscoveryRequestsPerDay();
            if (offset < 0 || limit < 1 || limit > 200 || name?.Length > 200)
                return BadRequest(new { error = "invalid_discovery_query", message = "Offset must be non-negative, limit 1–200, name at most 200 characters, and UUID filters non-empty." });
            try
            {
                var records = DiscoveryRecords.ReadPage<OSDC.Drilling.SurveyInstrument.Model.SurveyInstrumentIdentityLight>(_discoveryConnections.GetConnection(), "SurveyInstrumentIdentityTable", "SurveyInstrumentIdentity", offset, limit, name,
                    new System.Collections.Generic.Dictionary<string, object?> {  }, out int total);
                return Ok(new OSDC.Drilling.SurveyInstrument.Model.SurveyInstrumentIdentityDiscoveryPage { Items = records, Offset = offset, Limit = limit, TotalCount = total,
                    NextOffset = offset < total && records.Count < total - offset ? offset + records.Count : null });
            }
            catch (System.Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or System.Text.Json.JsonException or System.InvalidOperationException)
            {
                return StatusCode(500, new { error = "discovery_failed" });
            }
        }

    private readonly SurveyInstrumentIdentityManager manager;

    public SurveyInstrumentIdentityController(SqlConnectionManager connections) { _discoveryConnections = connections; manager = new(connections); }

    [HttpGet(Name = "GetAllSurveyInstrumentIdentityId")]
    public ActionResult<IEnumerable<Guid>> GetAllIds() => Ok(manager.GetAll().Select(value => value.MetaInfo!.ID));

    [HttpGet("MetaInfo", Name = "GetAllSurveyInstrumentIdentityMetaInfo")]
    public ActionResult<IEnumerable<MetaInfo?>> GetAllMetaInfo() => Ok(manager.GetAll().Select(value => value.MetaInfo));

    [NonAction]
        public ActionResult<IEnumerable<SurveyInstrumentIdentity>> GetAll() => Ok(manager.GetAll());

    [HttpGet("{id}", Name = "GetSurveyInstrumentIdentityById")]
    public ActionResult<SurveyInstrumentIdentity> Get(Guid id) => manager.Get(id) is { } value ? Ok(value) : NotFound();

    [HttpPost(Name = "PostSurveyInstrumentIdentity")]
    public ActionResult Post([FromBody] SurveyInstrumentIdentity? value) =>
        value?.MetaInfo?.ID is Guid id && id != Guid.Empty
            ? manager.Add(value) ? Ok(value) : Conflict()
            : BadRequest();

    [HttpPut("{id}", Name = "PutSurveyInstrumentIdentityById")]
    public ActionResult Put(Guid id, [FromQuery] DateTimeOffset expectedModifiedUtc,
        [FromBody] SurveyInstrumentIdentity? value)
    {
        SurveyInstrumentIdentity? current = manager.Get(id);
        if (value?.MetaInfo?.ID != id) return BadRequest();
        if (current == null) return NotFound();
        if (current.LastModificationDate != expectedModifiedUtc)
            return Conflict(new { error = "stale_write", currentModifiedUtc = current.LastModificationDate });
        return manager.Update(id, value) ? Ok(value) : Conflict();
    }

    [HttpDelete("{id}", Name = "DeleteSurveyInstrumentIdentityById")]
    public ActionResult Delete(Guid id, [FromQuery] DateTimeOffset expectedModifiedUtc)
    {
        SurveyInstrumentIdentity? current = manager.Get(id);
        if (current == null) return NotFound();
        if (current.LastModificationDate != expectedModifiedUtc)
            return Conflict(new { error = "stale_write", currentModifiedUtc = current.LastModificationDate });
        if (manager.IsReferenced(id)) return Conflict(new { error = "catalog_in_use" });
        return manager.Delete(id) ? Ok() : StatusCode(500);
    }
}
