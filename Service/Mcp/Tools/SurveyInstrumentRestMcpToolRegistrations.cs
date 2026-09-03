using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OSDC.Drilling.SurveyInstrument.Service.Controllers;
using OSDC.Drilling.SurveyInstrument.Service.Managers;
using ErrorSourceModel = OSDC.DotnetLibraries.Drilling.Surveying.ErrorSource;
using SurveyInstrumentModel = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument;

namespace OSDC.Drilling.SurveyInstrument.Service.Mcp.Tools;

public static class SurveyInstrumentRestMcpToolRegistrations
{
    public static IServiceCollection AddSurveyInstrumentRestMcpTools(this IServiceCollection services)
    {
        AddSurveyInstrumentTools(services);
        AddErrorSourceTools(services);
        return services;
    }

    private static void AddSurveyInstrumentTools(IServiceCollection services)
    {
        services.AddLegacyMcpTool("survey_instrument_get_all_ids", "List the UUIDs of every stored survey instrument. Use this compact discovery operation when only identifiers are needed, then pass one UUID to survey_instrument_get_by_id for the complete error-model definition.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => SurveyInstrumentController(sp).GetAllSurveyInstrumentId()));
        services.AddLegacyMcpTool("survey_instrument_get_all_meta_info", "List MetaInfo for every stored survey instrument without loading the complete models. Each result identifies a resource and may include host/base-path/endpoint metadata; use the ID with survey_instrument_get_by_id.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => SurveyInstrumentController(sp).GetAllSurveyInstrumentMetaInfo()));
        services.AddLegacyMcpTool("survey_instrument_get_by_id", "Retrieve one complete survey-instrument error model by UUID, including model family, embedded error sources, environmental parameters, gyro parameters, and Wolff-DeWardt settings. Physical quantities are returned in SI; angles are radians.", McpToolArgumentHelpers.CreateGuidSchema("id", "UUID of the survey instrument to retrieve."),
            (sp, args, ct) => InvokeById(args, ct, id => SurveyInstrumentController(sp).GetSurveyInstrumentById(id)));
        services.AddLegacyMcpTool("survey_instrument_get_all_light", "List lightweight survey-instrument records containing metadata, name, description, and timestamps. Use this for human-readable discovery; it omits model parameters and error-source details. Retrieve a selected full record by UUID afterward.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => SurveyInstrumentController(sp).GetAllSurveyInstrumentLight()));
        services.AddLegacyMcpTool("survey_instrument_get_all", "Retrieve every survey instrument with its complete error-model data, including embedded error sources and physical parameters. This can be a large response; prefer IDs, metadata, or light records for discovery. SI values are used and angles are radians.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => SurveyInstrumentController(sp).GetAllSurveyInstrument()));
        services.AddLegacyMcpTool("survey_instrument_create", "Persist a new complete survey-instrument error model. Generate a non-empty surveyInstrument.MetaInfo.ID first; an existing UUID produces a conflict. Select one of the four ModelType values, embed full ErrorSource objects when required, and supply physical values in SI with angles in radians.", McpToolArgumentHelpers.CreateSurveyInstrumentSchema(),
            (sp, args, ct) => InvokeWithBody<SurveyInstrumentModel>(args, "surveyInstrument", ct, data => SurveyInstrumentController(sp).PostSurveyInstrument(data)));
        services.AddLegacyMcpTool("survey_instrument_update_by_id", "Replace an existing survey-instrument definition. The path id must exactly match surveyInstrument.MetaInfo.ID or the request is rejected. Send the complete desired representation, retain fields that must not be lost, update LastModificationDate, and use SI values with angles in radians.", McpToolArgumentHelpers.CreateSurveyInstrumentSchema(includeId: true),
            (sp, args, ct) => InvokeWithIdAndBody<SurveyInstrumentModel>(args, "surveyInstrument", ct, (id, data) => SurveyInstrumentController(sp).PutSurveyInstrumentById(id, data)));
        services.AddLegacyMcpTool("survey_instrument_delete_by_id", "Permanently delete the stored survey instrument identified by UUID. Use a read operation first when the target is uncertain. The operation returns not found when the UUID does not identify an existing survey instrument.", McpToolArgumentHelpers.CreateGuidSchema("id", "UUID of the survey instrument to delete."),
            (sp, args, ct) => InvokeDelete(args, ct, id => SurveyInstrumentController(sp).DeleteSurveyInstrumentById(id)));
    }

    private static void AddErrorSourceTools(IServiceCollection services)
    {
        services.AddLegacyMcpTool("error_source_get_all_ids", "List the UUIDs of every independently stored survey error source. Use this compact discovery operation when only identifiers are needed, then retrieve a selected definition with error_source_get_by_id.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => ErrorSourceController(sp).GetAllErrorSourceId()));
        services.AddLegacyMcpTool("error_source_get_all_meta_info", "List MetaInfo for every independently stored error source without loading its model attributes. Each result supplies the UUID and may include host/base-path/endpoint metadata; use the UUID with error_source_get_by_id.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => ErrorSourceController(sp).GetAllErrorSourceMetaInfo()));
        services.AddLegacyMcpTool("error_source_get_by_id", "Retrieve one complete survey error-source definition by UUID, including ErrorCode, classification and correlation flags, magnitude, magnitude quantity, and optional inclination interval. Magnitude is expressed in the SI unit identified by MagnitudeQuantity; inclinations are radians.", McpToolArgumentHelpers.CreateGuidSchema("id", "UUID of the independently stored error source to retrieve."),
            (sp, args, ct) => InvokeById(args, ct, id => ErrorSourceController(sp).GetErrorSourceById(id)));
        services.AddLegacyMcpTool("error_source_get_all", "Retrieve every independently stored error-source definition with full classification, magnitude, quantity, and inclination-interval data. This can be a large response; prefer IDs or metadata for discovery. Magnitudes use their named quantity's SI unit.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => ErrorSourceController(sp).GetAllErrorSource()));
        services.AddLegacyMcpTool("error_source_create", "Persist a new reusable survey error-source definition. Generate a non-empty errorSource.MetaInfo.ID first; an existing UUID produces a conflict. Use an exact ErrorCode enum identifier and express Magnitude in the SI unit named by MagnitudeQuantity; inclination fields are radians.", McpToolArgumentHelpers.CreateErrorSourceSchema(),
            (sp, args, ct) => InvokeWithBody<ErrorSourceModel>(args, "errorSource", ct, data => ErrorSourceController(sp).PostErrorSource(data)));
        services.AddLegacyMcpTool("error_source_update_by_id", "Replace an existing independently stored error-source definition. The path id must exactly match errorSource.MetaInfo.ID or the request is rejected. Send the complete desired record, with Magnitude in the SI unit named by MagnitudeQuantity and inclination fields in radians.", McpToolArgumentHelpers.CreateErrorSourceSchema(includeId: true),
            (sp, args, ct) => InvokeWithIdAndBody<ErrorSourceModel>(args, "errorSource", ct, (id, data) => ErrorSourceController(sp).PutErrorSourceById(id, data)));
        services.AddLegacyMcpTool("error_source_delete_by_id", "Permanently delete the independently stored error source identified by UUID. Use a read operation first when the target is uncertain. This does not accept an ErrorCode in place of the resource UUID and returns not found for an unknown ID.", McpToolArgumentHelpers.CreateGuidSchema("id", "UUID of the independently stored error source to delete."),
            (sp, args, ct) => InvokeDelete(args, ct, id => ErrorSourceController(sp).DeleteErrorSourceById(id)));
    }

    private static Task<JsonNode?> Invoke<T>(CancellationToken cancellationToken, Func<ActionResult<T>> action)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action()));
    }

    private static Task<JsonNode?> InvokeById<T>(JsonObject? arguments, CancellationToken cancellationToken, Func<Guid, ActionResult<T>> action)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!McpToolArgumentHelpers.TryParseGuid(arguments, "id", out Guid id, out JsonNode? error))
        {
            return Task.FromResult(error);
        }

        return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(id)));
    }

    private static Task<JsonNode?> InvokeDelete(JsonObject? arguments, CancellationToken cancellationToken, Func<Guid, ActionResult> action)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!McpToolArgumentHelpers.TryParseGuid(arguments, "id", out Guid id, out JsonNode? error))
        {
            return Task.FromResult(error);
        }

        return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(id)));
    }

    private static Task<JsonNode?> InvokeWithBody<TModel>(JsonObject? arguments, string bodyName, CancellationToken cancellationToken, Func<TModel?, ActionResult> action)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryDeserialize(arguments, bodyName, out TModel? data, out JsonNode? error))
        {
            return Task.FromResult(error);
        }

        return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(data)));
    }

    private static Task<JsonNode?> InvokeWithIdAndBody<TModel>(JsonObject? arguments, string bodyName, CancellationToken cancellationToken, Func<Guid, TModel?, ActionResult> action)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!McpToolArgumentHelpers.TryParseGuid(arguments, "id", out Guid id, out JsonNode? idError))
        {
            return Task.FromResult(idError);
        }
        if (!TryDeserialize(arguments, bodyName, out TModel? data, out JsonNode? dataError))
        {
            return Task.FromResult(dataError);
        }

        return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(id, data)));
    }

    private static bool TryDeserialize<TModel>(JsonObject? arguments, string bodyName, out TModel? data, out JsonNode? error)
    {
        data = default;
        error = null;

        if (arguments?[bodyName] is not JsonNode node)
        {
            error = McpToolResponses.CreateValidationError($"Argument '{bodyName}' is required.");
            return false;
        }

        try
        {
            data = node.Deserialize<TModel>(JsonSettings.Options);
            if (data is null)
            {
                throw new InvalidOperationException();
            }
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            error = McpToolResponses.CreateValidationError($"Argument '{bodyName}' could not be deserialized.");
            return false;
        }
    }

    private static SurveyInstrumentController SurveyInstrumentController(IServiceProvider sp) =>
        new(
            sp.GetRequiredService<ILogger<SurveyInstrumentManager>>(),
            sp.GetRequiredService<ILogger<ErrorSourceManager>>(),
            sp.GetRequiredService<SqlConnectionManager>());

    private static ErrorSourceController ErrorSourceController(IServiceProvider sp) =>
        new(
            sp.GetRequiredService<ILogger<ErrorSourceManager>>(),
            sp.GetRequiredService<SqlConnectionManager>());
}
