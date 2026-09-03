using System;
using System.Collections.Generic;
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
using SurveyInstrumentModel = OSDC.Drilling.SurveyInstrument.Model.SurveyInstrument;
using IdentityModel = OSDC.Drilling.SurveyInstrument.Model.SurveyInstrumentIdentity;
using FeatureCategoryModel = OSDC.Drilling.SurveyInstrument.Model.SurveyInstrumentFeatureCategory;

namespace OSDC.Drilling.SurveyInstrument.Service.Mcp.Tools;

public static class SurveyInstrumentRestMcpToolRegistrations
{
    public static IServiceCollection AddSurveyInstrumentRestMcpTools(this IServiceCollection services)
    {
        AddSurveyInstrumentTools(services);
        AddErrorSourceTools(services);
        AddCatalogTools<IdentityModel>(services, "survey_instrument_identity", "surveyInstrumentIdentity", false,
            sp => IdentityController(sp).GetAll(), (sp, id) => IdentityController(sp).Get(id),
            (sp, value) => IdentityController(sp).Post(value),
            (sp, id, expected, value) => IdentityController(sp).Put(id, expected, value),
            (sp, id, expected) => IdentityController(sp).Delete(id, expected));
        AddCatalogTools<FeatureCategoryModel>(services, "survey_instrument_feature_category", "surveyInstrumentFeatureCategory", true,
            sp => FeatureController(sp).GetAll(), (sp, id) => FeatureController(sp).Get(id),
            (sp, value) => FeatureController(sp).Post(value),
            (sp, id, expected, value) => FeatureController(sp).Put(id, expected, value),
            (sp, id, expected) => FeatureController(sp).Delete(id, expected));
        return services;
    }

    private static void AddCatalogTools<T>(IServiceCollection services, string prefix, string bodyName, bool feature,
        Func<IServiceProvider, ActionResult<IEnumerable<T>>> all,
        Func<IServiceProvider, Guid, ActionResult<T>> get,
        Func<IServiceProvider, T?, ActionResult> create,
        Func<IServiceProvider, Guid, DateTimeOffset, T?, ActionResult> update,
        Func<IServiceProvider, Guid, DateTimeOffset, ActionResult> delete)
    {
        services.AddLegacyMcpTool(prefix + "_get_all", "List every survey-instrument catalog definition, including stable UUIDs, server-owned timestamps, names, and feature options where applicable. Read this catalog before assigning a definition to an instrument or attempting an optimistic-concurrency update.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => all(sp)));
        services.AddLegacyMcpTool(prefix + "_get_by_id", "Retrieve one complete survey-instrument catalog definition by stable UUID, including the latest LastModificationDate concurrency token and all feature options where applicable. An unknown UUID returns not found without changing service state.", McpToolArgumentHelpers.CreateGuidSchema("id", "Catalog definition UUID."),
            (sp, args, ct) => InvokeById(args, ct, id => get(sp, id)));
        services.AddLegacyMcpTool(prefix + "_create", "Create a custom survey-instrument catalog definition using a caller-generated non-empty UUID. The service owns CreationDate and LastModificationDate, rejects duplicate identifiers, and preserves category flags and stable option UUIDs for assignments.", McpToolArgumentHelpers.CreateCatalogSchema(bodyName, feature),
            (sp, args, ct) => InvokeWithBody<T>(args, bodyName, ct, value => create(sp, value)));
        services.AddLegacyMcpTool(prefix + "_update_by_id", "Replace one survey-instrument catalog definition. The path and body UUIDs must match, expectedModifiedUtc must equal the latest LastModificationDate, and options currently assigned to stored instruments cannot be removed.", McpToolArgumentHelpers.CreateCatalogSchema(bodyName, feature, true, true),
            (sp, args, ct) => InvokeCatalogUpdate<T>(args, bodyName, ct, (id, expected, value) => update(sp, id, expected, value)));
        services.AddLegacyMcpTool(prefix + "_delete_by_id", "Delete one unused survey-instrument catalog definition using its stable UUID and latest LastModificationDate. Referenced definitions and stale requests are rejected with conflict and leave instruments and catalogs unchanged.", McpToolArgumentHelpers.CreateCatalogDeleteSchema(),
            (sp, args, ct) => InvokeCatalogDelete(args, ct, (id, expected) => delete(sp, id, expected)));
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

    private static Task<JsonNode?> InvokeCatalogUpdate<T>(JsonObject? arguments, string bodyName,
        CancellationToken cancellationToken, Func<Guid, DateTimeOffset, T?, ActionResult> action)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!McpToolArgumentHelpers.TryParseGuid(arguments, "id", out Guid id, out JsonNode? error))
            return Task.FromResult(error);
        if (!DateTimeOffset.TryParse(arguments?["expectedModifiedUtc"]?.ToString(), out DateTimeOffset expected))
            return Task.FromResult<JsonNode?>(McpToolResponses.CreateValidationError(
                "Argument 'expectedModifiedUtc' must be an ISO 8601 date-time."));
        return TryDeserialize(arguments, bodyName, out T? value, out error)
            ? Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(id, expected, value)))
            : Task.FromResult(error);
    }

    private static Task<JsonNode?> InvokeCatalogDelete(JsonObject? arguments, CancellationToken cancellationToken,
        Func<Guid, DateTimeOffset, ActionResult> action)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!McpToolArgumentHelpers.TryParseGuid(arguments, "id", out Guid id, out JsonNode? error))
            return Task.FromResult(error);
        if (!DateTimeOffset.TryParse(arguments?["expectedModifiedUtc"]?.ToString(), out DateTimeOffset expected))
            return Task.FromResult<JsonNode?>(McpToolResponses.CreateValidationError(
                "Argument 'expectedModifiedUtc' must be an ISO 8601 date-time."));
        return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(id, expected)));
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

    private static SurveyInstrumentIdentityController IdentityController(IServiceProvider sp) =>
        new(sp.GetRequiredService<SqlConnectionManager>());

    private static SurveyInstrumentFeatureCategoryController FeatureController(IServiceProvider sp) =>
        new(sp.GetRequiredService<SqlConnectionManager>());
}
