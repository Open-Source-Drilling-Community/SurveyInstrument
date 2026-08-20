using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NORCE.Drilling.SurveyInstrument.Service.Controllers;
using NORCE.Drilling.SurveyInstrument.Service.Managers;
using ErrorSourceModel = OSDC.DotnetLibraries.Drilling.Surveying.ErrorSource;
using SurveyInstrumentModel = OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument;

namespace NORCE.Drilling.SurveyInstrument.Service.Mcp.Tools;

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
        services.AddLegacyMcpTool("survey_instrument.get_all_ids", "Retrieve all survey instrument identifiers.", null,
            (sp, _, ct) => Invoke(ct, () => SurveyInstrumentController(sp).GetAllSurveyInstrumentId()));
        services.AddLegacyMcpTool("survey_instrument.get_all_meta_info", "Retrieve metadata for all survey instruments.", null,
            (sp, _, ct) => Invoke(ct, () => SurveyInstrumentController(sp).GetAllSurveyInstrumentMetaInfo()));
        services.AddLegacyMcpTool("survey_instrument.get_by_id", "Retrieve a survey instrument by identifier.", McpToolArgumentHelpers.CreateGuidSchema("id"),
            (sp, args, ct) => InvokeById(args, ct, id => SurveyInstrumentController(sp).GetSurveyInstrumentById(id)));
        services.AddLegacyMcpTool("survey_instrument.get_all_light", "Retrieve all survey instruments as lightweight records.", null,
            (sp, _, ct) => Invoke(ct, () => SurveyInstrumentController(sp).GetAllSurveyInstrumentLight()));
        services.AddLegacyMcpTool("survey_instrument.get_all", "Retrieve all survey instruments with full data.", null,
            (sp, _, ct) => Invoke(ct, () => SurveyInstrumentController(sp).GetAllSurveyInstrument()));
        services.AddLegacyMcpTool("survey_instrument.create", "Create a survey instrument.", McpToolArgumentHelpers.CreateObjectSchema("surveyInstrument"),
            (sp, args, ct) => InvokeWithBody<SurveyInstrumentModel>(args, "surveyInstrument", ct, data => SurveyInstrumentController(sp).PostSurveyInstrument(data)));
        services.AddLegacyMcpTool("survey_instrument.update_by_id", "Update a survey instrument identified by id.", McpToolArgumentHelpers.CreateObjectSchema("surveyInstrument", includeId: true),
            (sp, args, ct) => InvokeWithIdAndBody<SurveyInstrumentModel>(args, "surveyInstrument", ct, (id, data) => SurveyInstrumentController(sp).PutSurveyInstrumentById(id, data)));
        services.AddLegacyMcpTool("survey_instrument.delete_by_id", "Delete a survey instrument by identifier.", McpToolArgumentHelpers.CreateGuidSchema("id"),
            (sp, args, ct) => InvokeDelete(args, ct, id => SurveyInstrumentController(sp).DeleteSurveyInstrumentById(id)));
    }

    private static void AddErrorSourceTools(IServiceCollection services)
    {
        services.AddLegacyMcpTool("error_source.get_all_ids", "Retrieve all error source identifiers.", null,
            (sp, _, ct) => Invoke(ct, () => ErrorSourceController(sp).GetAllErrorSourceId()));
        services.AddLegacyMcpTool("error_source.get_all_meta_info", "Retrieve metadata for all error sources.", null,
            (sp, _, ct) => Invoke(ct, () => ErrorSourceController(sp).GetAllErrorSourceMetaInfo()));
        services.AddLegacyMcpTool("error_source.get_by_id", "Retrieve an error source by identifier.", McpToolArgumentHelpers.CreateGuidSchema("id"),
            (sp, args, ct) => InvokeById(args, ct, id => ErrorSourceController(sp).GetErrorSourceById(id)));
        services.AddLegacyMcpTool("error_source.get_all", "Retrieve all error sources with full data.", null,
            (sp, _, ct) => Invoke(ct, () => ErrorSourceController(sp).GetAllErrorSource()));
        services.AddLegacyMcpTool("error_source.create", "Create an error source.", McpToolArgumentHelpers.CreateObjectSchema("errorSource"),
            (sp, args, ct) => InvokeWithBody<ErrorSourceModel>(args, "errorSource", ct, data => ErrorSourceController(sp).PostErrorSource(data)));
        services.AddLegacyMcpTool("error_source.update_by_id", "Update an error source identified by id.", McpToolArgumentHelpers.CreateObjectSchema("errorSource", includeId: true),
            (sp, args, ct) => InvokeWithIdAndBody<ErrorSourceModel>(args, "errorSource", ct, (id, data) => ErrorSourceController(sp).PutErrorSourceById(id, data)));
        services.AddLegacyMcpTool("error_source.delete_by_id", "Delete an error source by identifier.", McpToolArgumentHelpers.CreateGuidSchema("id"),
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
