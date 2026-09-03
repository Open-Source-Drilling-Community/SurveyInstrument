using Microsoft.AspNetCore.Mvc;
using OSDC.Drilling.SurveyInstrument.Model;
using OSDC.Drilling.SurveyInstrument.Service.Managers;
using OSDC.DotnetLibraries.General.DataManagement;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OSDC.Drilling.SurveyInstrument.Service.Controllers;

[Produces("application/json"), Route("[controller]"), ApiController]
public class SurveyInstrumentFeatureCategoryController : ControllerBase
{
    private readonly SurveyInstrumentFeatureCategoryManager manager;

    public SurveyInstrumentFeatureCategoryController(SqlConnectionManager connections) => manager = new(connections);

    [HttpGet(Name = "GetAllSurveyInstrumentFeatureCategoryId")]
    public ActionResult<IEnumerable<Guid>> GetAllIds() => Ok(manager.GetAll().Select(value => value.MetaInfo!.ID));

    [HttpGet("MetaInfo", Name = "GetAllSurveyInstrumentFeatureCategoryMetaInfo")]
    public ActionResult<IEnumerable<MetaInfo?>> GetAllMetaInfo() => Ok(manager.GetAll().Select(value => value.MetaInfo));

    [HttpGet("HeavyData", Name = "GetAllSurveyInstrumentFeatureCategory")]
    public ActionResult<IEnumerable<SurveyInstrumentFeatureCategory>> GetAll() => Ok(manager.GetAll());

    [HttpGet("{id}", Name = "GetSurveyInstrumentFeatureCategoryById")]
    public ActionResult<SurveyInstrumentFeatureCategory> Get(Guid id) => manager.Get(id) is { } value ? Ok(value) : NotFound();

    [HttpPost(Name = "PostSurveyInstrumentFeatureCategory")]
    public ActionResult Post([FromBody] SurveyInstrumentFeatureCategory? value) =>
        value?.MetaInfo?.ID is Guid id && id != Guid.Empty
            ? manager.Add(value) ? Ok(value) : Conflict()
            : BadRequest();

    [HttpPut("{id}", Name = "PutSurveyInstrumentFeatureCategoryById")]
    public ActionResult Put(Guid id, [FromQuery] DateTimeOffset expectedModifiedUtc,
        [FromBody] SurveyInstrumentFeatureCategory? value)
    {
        SurveyInstrumentFeatureCategory? current = manager.Get(id);
        if (value?.MetaInfo?.ID != id) return BadRequest();
        if (current == null) return NotFound();
        if (current.LastModificationDate != expectedModifiedUtc)
            return Conflict(new { error = "stale_write", currentModifiedUtc = current.LastModificationDate });
        return manager.Update(id, value) ? Ok(value) : Conflict(new { error = "catalog_in_use_or_invalid" });
    }

    [HttpDelete("{id}", Name = "DeleteSurveyInstrumentFeatureCategoryById")]
    public ActionResult Delete(Guid id, [FromQuery] DateTimeOffset expectedModifiedUtc)
    {
        SurveyInstrumentFeatureCategory? current = manager.Get(id);
        if (current == null) return NotFound();
        if (current.LastModificationDate != expectedModifiedUtc)
            return Conflict(new { error = "stale_write", currentModifiedUtc = current.LastModificationDate });
        if (manager.IsReferenced(id)) return Conflict(new { error = "catalog_in_use" });
        return manager.Delete(id) ? Ok() : StatusCode(500);
    }
}
