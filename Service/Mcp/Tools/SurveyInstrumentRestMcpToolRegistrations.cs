using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
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
using BatchExportRequestModel = OSDC.Drilling.SurveyInstrument.Model.SurveyInstrumentBatchExportRequest;
using BatchExportDocumentModel = OSDC.Drilling.SurveyInstrument.Model.SurveyInstrumentBatchExportDocument;
using BatchRestoreRequestModel = OSDC.Drilling.SurveyInstrument.Model.SurveyInstrumentBatchRestoreRequest;
using BatchRestoreResponseModel = OSDC.Drilling.SurveyInstrument.Model.SurveyInstrumentBatchRestoreResponse;

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
        services.AddLegacyMcpTool("survey_instrument_check_error_source_drift", "Compare every frozen ErrorSourceList snapshot in one survey instrument with the current standalone template carrying the same MetaInfo.ID. The read-only result reports in_sync, drifted, or catalog_missing per UUID and never modifies the instrument or template catalog.", McpToolArgumentHelpers.CreateGuidSchema("id", "UUID of the survey instrument whose embedded snapshots should be checked."),
            InvokeErrorSourceDriftCheck);
        services.AddLegacyMcpTool("survey_instrument_validate_catalog_references", "Validate one stored survey instrument's identity, feature-category, and feature-option assignment UUIDs against the local Survey Instrument catalogs without changing data. Normal writes already enforce these references; this diagnostic detects legacy, imported, or externally corrupted records and returns bounded issue codes.", McpToolArgumentHelpers.CreateGuidSchema("id", "UUID of the survey instrument whose local catalog assignments should be validated."),
            InvokeCatalogReferenceValidation);
        services.AddLegacyMcpTool("survey_instrument_audit_catalog_references", "Validate a deterministic, bounded page of stored survey instruments against the local identity and feature catalogs without changing data. Results identify missing identity definitions, feature categories, and category-scoped options, allowing legacy or imported catalog corruption to be audited without loading every complete record into the MCP response.", McpToolArgumentHelpers.CreateCatalogReferenceAuditSchema(),
            InvokeCatalogReferenceAudit);
        services.AddLegacyMcpTool("survey_instrument_get_all_light", "List lightweight survey-instrument records containing metadata, name, description, and timestamps. Use this for human-readable discovery; it omits model parameters and error-source details. Retrieve a selected full record by UUID afterward.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => SurveyInstrumentController(sp).GetAllSurveyInstrumentLight()));
        services.AddLegacyMcpTool("survey_instrument_get_all", "Retrieve every survey instrument with its complete error-model data, including embedded error sources and physical parameters. This can be a large response; prefer IDs, metadata, or light records for discovery. SI values are used and angles are radians.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => SurveyInstrumentController(sp).GetAllSurveyInstrument()));
        services.AddLegacyMcpTool("survey_instrument_batch_export", "Create a read-only schema-version-1 logical backup of all survey instruments or an explicit ordered selection. The document contains complete frozen instrument records, referenced identity and feature definitions, and applicable error-source templates. A missing selected UUID rejects the entire export.", McpToolArgumentHelpers.CreateBatchExportSchema(),
            (sp, args, ct) => InvokeWithBodyResult<BatchExportRequestModel, BatchExportDocumentModel>(args, "request", ct,
                request => SurveyInstrumentController(sp).BatchExportSurveyInstruments(request)));
        services.AddLegacyMcpTool("survey_instrument_batch_restore", "Validate and atomically restore a schema-version-1 Survey Instrument backup. FailIfExists preserves every existing instrument; ReplaceExisting replaces matching instrument UUIDs. Missing catalog dependencies are created by exact UUID, while differing content at an existing catalog UUID rejects the whole restore.", McpToolArgumentHelpers.CreateBatchRestoreSchema(),
            (sp, args, ct) => InvokeWithBodyResult<BatchRestoreRequestModel, BatchRestoreResponseModel>(args, "request", ct,
                request => SurveyInstrumentController(sp).BatchRestoreSurveyInstruments(request)));
        services.AddLegacyMcpTool("survey_instrument_create", "Persist a new complete survey-instrument error model. Generate a non-empty surveyInstrument.MetaInfo.ID first; an existing UUID produces a conflict. Select one of the four discriminated ModelType families, embed authoritative ErrorSource snapshots when required, and supply physical values in SI with angles in radians.", McpToolArgumentHelpers.CreateSurveyInstrumentSchema(),
            (sp, args, ct) => InvokeWithBody<SurveyInstrumentModel>(args, "surveyInstrument", ct, data => SurveyInstrumentController(sp).PostSurveyInstrument(data)));
        services.AddLegacyMcpTool("survey_instrument_update_by_id", "Replace an existing survey-instrument definition with optimistic concurrency protection. The path id must match surveyInstrument.MetaInfo.ID and expectedModifiedUtc must equal the latest LastModificationDate. Send the complete desired representation; a stale request returns stale_write without changing data.", McpToolArgumentHelpers.CreateSurveyInstrumentSchema(includeId: true),
            InvokeSurveyInstrumentUpdate);
        services.AddLegacyMcpTool("survey_instrument_patch_by_id", "Partially update one survey instrument with optimistic concurrency protection. Supply only changed top-level fields in patch; omitted fields are retained, arrays are replaced as a whole, and null clears nullable fields. MetaInfo and server timestamps cannot be patched. A stale expectedModifiedUtc returns stale_write.", McpToolArgumentHelpers.CreateSurveyInstrumentPatchSchema(),
            InvokeSurveyInstrumentPatch);
        services.AddLegacyMcpTool("survey_instrument_error_source_mutate", "Add, replace, or remove one embedded ErrorSourceList snapshot without resending the complete array. The operation is atomic and requires the latest survey-instrument LastModificationDate as expectedModifiedUtc. Add rejects a duplicate snapshot UUID, replace and remove reject an unknown snapshot UUID, and family semantics still prevent an ISCWSA instrument from ending with an empty list.", McpToolArgumentHelpers.CreateErrorSourceSnapshotMutationSchema(),
            InvokeErrorSourceSnapshotMutation);
        services.AddLegacyMcpTool("survey_instrument_delete_by_id", "Permanently delete a stored survey instrument with optimistic concurrency protection. expectedModifiedUtc must equal the LastModificationDate from the latest read; an unknown UUID returns not_found and a stale request returns stale_write without deleting data.", McpToolArgumentHelpers.CreateSurveyInstrumentDeleteSchema(),
            InvokeSurveyInstrumentDelete);
    }

    private static void AddErrorSourceTools(IServiceCollection services)
    {
        services.AddLegacyMcpTool("error_source_get_all_ids", "List the UUIDs of every independently stored survey error source. Use this compact discovery operation when only identifiers are needed, then retrieve a selected definition with error_source_get_by_id.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => ErrorSourceController(sp).GetAllErrorSourceId()));
        services.AddLegacyMcpTool("error_source_get_all_meta_info", "List MetaInfo for every independently stored error source without loading its model attributes. Each result supplies the UUID and may include host/base-path/endpoint metadata; use the UUID with error_source_get_by_id.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => ErrorSourceController(sp).GetAllErrorSourceMetaInfo()));
        services.AddLegacyMcpTool("error_source_get_by_id", "Retrieve one complete error-source template by UUID, including ErrorCode, classification and correlation flags, magnitude, quantity, and optional inclination interval. Copying it into an instrument creates an authoritative snapshot with UUID provenance; later template updates do not propagate. SI units are used.", McpToolArgumentHelpers.CreateGuidSchema("id", "UUID of the independently stored error-source template to retrieve."),
            InvokeErrorSourceGetById);
        services.AddLegacyMcpTool("error_source_get_all", "Retrieve every independently stored error-source definition with full classification, magnitude, quantity, and inclination-interval data. This can be a large response; prefer IDs or metadata for discovery. Magnitudes use their named quantity's SI unit.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => ErrorSourceController(sp).GetAllErrorSource()));
        services.AddLegacyMcpTool("error_source_create", "Persist a new reusable survey error-source template. Generate a non-empty errorSource.MetaInfo.ID first; an existing UUID produces a conflict. Instruments embed authoritative snapshots rather than live references, so later template updates do not propagate. Use the named quantity's SI unit.", McpToolArgumentHelpers.CreateErrorSourceSchema(),
            (sp, args, ct) => InvokeWithBody<ErrorSourceModel>(args, "errorSource", ct, data => ErrorSourceController(sp).PostErrorSource(data)));
        services.AddLegacyMcpTool("error_source_update_by_id", "Replace an existing independently stored error-source definition with optimistic concurrency. The path id must match errorSource.MetaInfo.ID and expectedVersionToken must equal the token from the latest error_source_get_by_id read. The success result returns a new token and warns when stored instruments contain same-UUID frozen snapshots; those snapshots remain unchanged. Magnitude uses the named quantity's SI unit and inclination fields use radians.", McpToolArgumentHelpers.CreateErrorSourceSchema(includeId: true, includeExpected: true),
            InvokeErrorSourceUpdate);
        services.AddLegacyMcpTool("error_source_delete_by_id", "Permanently delete an independently stored error-source template with optimistic concurrency. expectedVersionToken must equal the token from the latest error_source_get_by_id read; an unknown UUID returns not_found and a changed template returns stale_write without deletion. Embedded snapshots are independent and are not deleted.", McpToolArgumentHelpers.CreateErrorSourceDeleteSchema(),
            InvokeErrorSourceDelete);
    }

    private static Task<JsonNode?> InvokeSurveyInstrumentUpdate(
        IServiceProvider serviceProvider, JsonObject? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetSurveyInstrumentWriteContext(serviceProvider, arguments, out Guid id,
                out DateTimeOffset expected, out SurveyInstrumentController? controller,
                out SurveyInstrumentModel? current, out JsonNode? error))
        {
            return Task.FromResult(error);
        }
        if (!TryDeserialize(arguments, "surveyInstrument", out SurveyInstrumentModel? data, out error))
        {
            return Task.FromResult(error);
        }

        return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(
            controller!.PutSurveyInstrumentById(id, data, expected)));
    }

    private static Task<JsonNode?> InvokeErrorSourceDriftCheck(
        IServiceProvider serviceProvider, JsonObject? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!McpToolArgumentHelpers.TryParseGuid(arguments, "id", out Guid id, out JsonNode? error))
            return Task.FromResult(error);
        ActionResult<SurveyInstrumentModel?> lookup = SurveyInstrumentController(serviceProvider).GetSurveyInstrumentById(id);
        SurveyInstrumentModel? instrument = lookup.Value ?? (lookup.Result as ObjectResult)?.Value as SurveyInstrumentModel;
        if (instrument == null) return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(lookup));

        ErrorSourceController catalog = ErrorSourceController(serviceProvider);
        JsonArray results = [];
        bool hasDrift = false;
        foreach (ErrorSourceModel snapshot in instrument.ErrorSourceList ?? [])
        {
            Guid sourceId = snapshot.MetaInfo?.ID ?? Guid.Empty;
            ActionResult<ErrorSourceModel?> templateLookup = catalog.GetErrorSourceById(sourceId);
            ErrorSourceModel? template = templateLookup.Value ?? (templateLookup.Result as ObjectResult)?.Value as ErrorSourceModel;
            string status = template == null ? "catalog_missing" :
                JsonNode.DeepEquals(JsonSerializer.SerializeToNode(snapshot, JsonSettings.Options),
                    JsonSerializer.SerializeToNode(template, JsonSettings.Options)) ? "in_sync" : "drifted";
            hasDrift |= status != "in_sync";
            results.Add(new JsonObject { ["ErrorSourceID"] = sourceId.ToString(), ["Status"] = status });
        }
        return Task.FromResult<JsonNode?>(new JsonObject
        {
            ["status"] = 200,
            ["data"] = new JsonObject
            {
                ["SurveyInstrumentID"] = id.ToString(), ["HasDrift"] = hasDrift, ["Results"] = results
            }
        });
    }

    private static Task<JsonNode?> InvokeCatalogReferenceValidation(
        IServiceProvider serviceProvider, JsonObject? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!McpToolArgumentHelpers.TryParseGuid(arguments, "id", out Guid id, out JsonNode? error))
            return Task.FromResult(error);
        ActionResult<SurveyInstrumentModel?> lookup = SurveyInstrumentController(serviceProvider).GetSurveyInstrumentById(id);
        SurveyInstrumentModel? instrument = lookup.Value ?? (lookup.Result as ObjectResult)?.Value as SurveyInstrumentModel;
        if (instrument == null) return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(lookup));
        CatalogReferenceSets catalogs = GetCatalogReferenceSets(serviceProvider);
        return Task.FromResult<JsonNode?>(Success(ValidateCatalogReferences(instrument, catalogs)));
    }

    private static Task<JsonNode?> InvokeCatalogReferenceAudit(
        IServiceProvider serviceProvider, JsonObject? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetBoundedInteger(arguments, "offset", 0, 0, int.MaxValue, out int offset, out JsonNode? error) ||
            !TryGetBoundedInteger(arguments, "limit", 50, 1, 100, out int limit, out error))
            return Task.FromResult(error);

        ActionResult<IEnumerable<SurveyInstrumentModel?>> action = SurveyInstrumentController(serviceProvider).GetAllSurveyInstrument();
        IEnumerable<SurveyInstrumentModel?>? values = action.Value ??
            (action.Result as ObjectResult)?.Value as IEnumerable<SurveyInstrumentModel?>;
        if (values == null) return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action));

        SurveyInstrumentModel[] ordered = values.Where(value => value?.MetaInfo != null).Cast<SurveyInstrumentModel>()
            .OrderBy(value => value.MetaInfo!.ID).ToArray();
        CatalogReferenceSets catalogs = GetCatalogReferenceSets(serviceProvider);
        JsonArray results = new(ordered.Skip(offset).Take(limit).Select(value =>
            (JsonNode?)ValidateCatalogReferences(value, catalogs)).ToArray());
        int invalid = results.Count(value => value?["Status"]?.GetValue<string>() == "invalid");
        return Task.FromResult<JsonNode?>(Success(new JsonObject
        {
            ["Offset"] = offset,
            ["Limit"] = limit,
            ["CheckedCount"] = results.Count,
            ["ValidCount"] = results.Count - invalid,
            ["InvalidCount"] = invalid,
            ["HasMore"] = offset + results.Count < ordered.Length,
            ["Results"] = results
        }));
    }

    private static Task<JsonNode?> InvokeErrorSourceUpdate(
        IServiceProvider serviceProvider, JsonObject? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!McpToolArgumentHelpers.TryParseGuid(arguments, "id", out Guid id, out JsonNode? error))
            return Task.FromResult(error);
        if (!TryGetExpectedVersionToken(arguments, out string expectedToken, out error))
            return Task.FromResult(error);
        if (!TryDeserialize(arguments, "errorSource", out ErrorSourceModel? data, out error))
            return Task.FromResult(error);

        ErrorSourceController errorSources = ErrorSourceController(serviceProvider);
        ActionResult<ErrorSourceModel?> currentAction = errorSources.GetErrorSourceById(id);
        ErrorSourceModel? current = currentAction.Value ?? (currentAction.Result as ObjectResult)?.Value as ErrorSourceModel;
        if (current == null) return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(currentAction));
        string expectedCurrentJson = SerializeErrorSource(current);
        if (!string.Equals(ComputeVersionToken(expectedCurrentJson), expectedToken, StringComparison.Ordinal))
            return Task.FromResult<JsonNode?>(McpToolResponses.CreateConflict("stale_write", "The error-source template changed after it was read. Read it again and retry with the latest versionToken."));

        ActionResult<IEnumerable<SurveyInstrumentModel?>> allAction = SurveyInstrumentController(serviceProvider).GetAllSurveyInstrument();
        IEnumerable<SurveyInstrumentModel?> values = allAction.Value ??
            (allAction.Result as ObjectResult)?.Value as IEnumerable<SurveyInstrumentModel?> ?? [];
        SurveyInstrumentModel[] affectedInstruments = values
            .Where(value => value?.ErrorSourceList?.Any(source => source.MetaInfo?.ID == id) == true)
            .Cast<SurveyInstrumentModel>().ToArray();
        Guid[] affected = affectedInstruments
            .Select(value => value!.MetaInfo!.ID).Distinct().OrderBy(value => value).ToArray();
        int affectedSnapshotCount = affectedInstruments.Sum(value =>
            value.ErrorSourceList?.Count(source => source.MetaInfo?.ID == id) ?? 0);

        ActionResult update = errorSources.PutErrorSourceById(id, data, expectedCurrentJson);
        JsonNode? converted = McpActionResultConverter.FromActionResult(update);
        if (converted?["status"]?.GetValue<int>() != 200) return Task.FromResult(converted);
        return Task.FromResult<JsonNode?>(Success(new JsonObject
        {
            ["ErrorSourceID"] = id.ToString(),
            ["VersionToken"] = ComputeVersionToken(SerializeErrorSource(data!)),
            ["AffectedSnapshotCount"] = affectedSnapshotCount,
            ["AffectedSurveyInstrumentIDs"] = new JsonArray(affected.Select(value => (JsonNode?)value.ToString()).ToArray()),
            ["Warning"] = affected.Length == 0 ? null :
                $"{affectedSnapshotCount} frozen snapshot(s) across {affected.Length} survey instrument(s) were not modified."
        }));
    }

    private static Task<JsonNode?> InvokeErrorSourceGetById(
        IServiceProvider serviceProvider, JsonObject? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!McpToolArgumentHelpers.TryParseGuid(arguments, "id", out Guid id, out JsonNode? error))
            return Task.FromResult(error);
        ActionResult<ErrorSourceModel?> action = ErrorSourceController(serviceProvider).GetErrorSourceById(id);
        ErrorSourceModel? source = action.Value ?? (action.Result as ObjectResult)?.Value as ErrorSourceModel;
        JsonObject response = McpActionResultConverter.FromActionResult(action);
        if (source != null) response["versionToken"] = ComputeVersionToken(SerializeErrorSource(source));
        return Task.FromResult<JsonNode?>(response);
    }

    private static Task<JsonNode?> InvokeErrorSourceDelete(
        IServiceProvider serviceProvider, JsonObject? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!McpToolArgumentHelpers.TryParseGuid(arguments, "id", out Guid id, out JsonNode? error))
            return Task.FromResult(error);
        if (!TryGetExpectedVersionToken(arguments, out string expectedToken, out error))
            return Task.FromResult(error);
        ErrorSourceController controller = ErrorSourceController(serviceProvider);
        ActionResult<ErrorSourceModel?> currentAction = controller.GetErrorSourceById(id);
        ErrorSourceModel? current = currentAction.Value ?? (currentAction.Result as ObjectResult)?.Value as ErrorSourceModel;
        if (current == null) return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(currentAction));
        string expectedCurrentJson = SerializeErrorSource(current);
        if (!string.Equals(ComputeVersionToken(expectedCurrentJson), expectedToken, StringComparison.Ordinal))
            return Task.FromResult<JsonNode?>(McpToolResponses.CreateConflict("stale_write", "The error-source template changed after it was read. Read it again and retry with the latest versionToken."));
        return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(
            controller.DeleteErrorSourceById(id, expectedCurrentJson)));
    }

    private static bool TryGetExpectedVersionToken(JsonObject? arguments, out string token, out JsonNode? error)
    {
        token = arguments?["expectedVersionToken"]?.ToString() ?? string.Empty;
        if (token.Length == 64 && token.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            error = null;
            return true;
        }
        error = McpToolResponses.CreateValidationError("Argument 'expectedVersionToken' must be the 64-character lowercase SHA-256 token returned by error_source_get_by_id.");
        return false;
    }

    private static string SerializeErrorSource(ErrorSourceModel source) =>
        JsonSerializer.Serialize(source, JsonSettings.Options);

    private static string ComputeVersionToken(string serialized) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized))).ToLowerInvariant();

    private static CatalogReferenceSets GetCatalogReferenceSets(IServiceProvider serviceProvider)
    {
        SqlConnectionManager connections = serviceProvider.GetRequiredService<SqlConnectionManager>();
        HashSet<Guid> identities = new SurveyInstrumentIdentityManager(connections).GetAll()
            .Where(value => value.MetaInfo != null).Select(value => value.MetaInfo!.ID).ToHashSet();
        Dictionary<Guid, HashSet<Guid>> features = new SurveyInstrumentFeatureCategoryManager(connections).GetAll()
            .Where(value => value.MetaInfo != null).ToDictionary(value => value.MetaInfo!.ID,
                value => (value.Options ?? []).Select(option => option.ID).ToHashSet());
        return new CatalogReferenceSets(identities, features);
    }

    private static JsonObject ValidateCatalogReferences(SurveyInstrumentModel instrument, CatalogReferenceSets catalogs)
    {
        JsonArray issues = [];
        int index = 0;
        foreach (Model.SurveyInstrumentIdentityAssignment assignment in instrument.SurveyInstrumentIdentityAssignments ?? [])
        {
            if (assignment.IdentityID is not Guid id || !catalogs.Identities.Contains(id))
                issues.Add(CatalogIssue($"SurveyInstrumentIdentityAssignments[{index}].IdentityID", "identity_missing", assignment.IdentityID));
            index++;
        }
        index = 0;
        foreach (Model.SurveyInstrumentFeatureAssignment assignment in instrument.SurveyInstrumentFeatureAssignments ?? [])
        {
            if (assignment.FeatureCategoryID is not Guid categoryId || !catalogs.Features.TryGetValue(categoryId, out HashSet<Guid>? options))
                issues.Add(CatalogIssue($"SurveyInstrumentFeatureAssignments[{index}].FeatureCategoryID", "feature_category_missing", assignment.FeatureCategoryID));
            else if (assignment.FeatureOptionID is not Guid optionId || !options.Contains(optionId))
                issues.Add(CatalogIssue($"SurveyInstrumentFeatureAssignments[{index}].FeatureOptionID", "feature_option_missing", assignment.FeatureOptionID));
            index++;
        }
        return new JsonObject
        {
            ["SurveyInstrumentID"] = instrument.MetaInfo!.ID.ToString(),
            ["Status"] = issues.Count == 0 ? "valid" : "invalid",
            ["Issues"] = issues
        };
    }

    private static JsonObject CatalogIssue(string path, string code, Guid? referencedId) => new()
    {
        ["Path"] = path, ["Code"] = code, ["ReferencedID"] = (referencedId ?? Guid.Empty).ToString()
    };

    private static bool TryGetBoundedInteger(JsonObject? arguments, string name, int defaultValue,
        int minimum, int maximum, out int value, out JsonNode? error)
    {
        value = defaultValue;
        error = null;
        if (arguments?[name] == null) return true;
        if (!int.TryParse(arguments[name]!.ToString(), out value) || value < minimum || value > maximum)
        {
            error = McpToolResponses.CreateValidationError($"Argument '{name}' must be an integer from {minimum} through {maximum}.");
            return false;
        }
        return true;
    }

    private static JsonObject Success(JsonNode data) => new() { ["status"] = 200, ["data"] = data };

    private sealed record CatalogReferenceSets(HashSet<Guid> Identities, Dictionary<Guid, HashSet<Guid>> Features);

    private static Task<JsonNode?> InvokeSurveyInstrumentPatch(
        IServiceProvider serviceProvider, JsonObject? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetSurveyInstrumentWriteContext(serviceProvider, arguments, out Guid id,
                out DateTimeOffset expected, out SurveyInstrumentController? controller,
                out SurveyInstrumentModel? current, out JsonNode? error))
        {
            return Task.FromResult(error);
        }
        if (arguments?["patch"] is not JsonObject patch || patch.Count == 0)
        {
            return Task.FromResult<JsonNode?>(McpToolResponses.CreateValidationError(
                "Argument 'patch' must be a non-empty object."));
        }

        JsonObject merged = JsonSerializer.SerializeToNode(current, JsonSettings.Options)!.AsObject();
        IReadOnlySet<string> allowed = McpToolArgumentHelpers.SurveyInstrumentPatchFields;
        foreach ((string name, JsonNode? value) in patch)
        {
            if (!allowed.Contains(name))
            {
                return Task.FromResult<JsonNode?>(McpToolResponses.CreateValidationError(
                    $"Field '{name}' cannot be patched."));
            }
            merged[name] = value?.DeepClone();
        }

        try
        {
            SurveyInstrumentModel? updated = merged.Deserialize<SurveyInstrumentModel>(JsonSettings.Options);
            if (updated is null)
            {
                throw new JsonException();
            }
            return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(
                controller!.PutSurveyInstrumentById(id, updated, expected)));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return Task.FromResult<JsonNode?>(McpToolResponses.CreateValidationError(
                "Argument 'patch' contains a value incompatible with the survey-instrument model."));
        }
    }

    private static Task<JsonNode?> InvokeSurveyInstrumentDelete(
        IServiceProvider serviceProvider, JsonObject? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetSurveyInstrumentWriteContext(serviceProvider, arguments, out Guid id,
                out DateTimeOffset expected, out SurveyInstrumentController? controller,
                out _, out JsonNode? error))
        {
            return Task.FromResult(error);
        }
        return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(
            controller!.DeleteSurveyInstrumentById(id, expected)));
    }

    private static Task<JsonNode?> InvokeErrorSourceSnapshotMutation(
        IServiceProvider serviceProvider, JsonObject? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetSurveyInstrumentWriteContext(serviceProvider, arguments, out Guid id,
                out DateTimeOffset expected, out SurveyInstrumentController? controller,
                out SurveyInstrumentModel? current, out JsonNode? error))
            return Task.FromResult(error);

        string? operation = arguments?["operation"]?.GetValue<string>();
        List<ErrorSourceModel> snapshots = (current!.ErrorSourceList ?? []).ToList();
        if (operation is "add" or "replace")
        {
            if (!TryDeserialize(arguments, "errorSource", out ErrorSourceModel? snapshot, out error))
                return Task.FromResult(error);
            Guid snapshotId = snapshot!.MetaInfo?.ID ?? Guid.Empty;
            if (snapshotId == Guid.Empty)
                return Task.FromResult<JsonNode?>(McpToolResponses.CreateValidationError("errorSource.MetaInfo.ID must be a non-empty UUID."));
            int index = snapshots.FindIndex(value => value.MetaInfo?.ID == snapshotId);
            if (operation == "add" && index >= 0)
                return Task.FromResult<JsonNode?>(McpToolResponses.CreateConflict("snapshot_exists", "An embedded error-source snapshot with this UUID already exists."));
            if (operation == "replace" && index < 0)
                return Task.FromResult<JsonNode?>(McpToolResponses.CreateConflict("snapshot_not_found", "No embedded error-source snapshot with this UUID exists."));
            if (operation == "add") snapshots.Add(snapshot);
            else snapshots[index] = snapshot;
        }
        else if (operation == "remove")
        {
            if (!McpToolArgumentHelpers.TryParseGuid(arguments, "errorSourceId", out Guid snapshotId, out error))
                return Task.FromResult(error);
            int index = snapshots.FindIndex(value => value.MetaInfo?.ID == snapshotId);
            if (index < 0)
                return Task.FromResult<JsonNode?>(McpToolResponses.CreateConflict("snapshot_not_found", "No embedded error-source snapshot with this UUID exists."));
            snapshots.RemoveAt(index);
        }
        else
        {
            return Task.FromResult<JsonNode?>(McpToolResponses.CreateValidationError("Argument 'operation' must be add, replace, or remove."));
        }

        current.ErrorSourceList = snapshots;
        JsonObject updated = McpActionResultConverter.FromActionResult(
            controller!.PutSurveyInstrumentById(id, current, expected));
        return updated["status"]?.GetValue<int>() == 200
            ? Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(controller.GetSurveyInstrumentById(id)))
            : Task.FromResult<JsonNode?>(updated);
    }

    private static bool TryGetSurveyInstrumentWriteContext(
        IServiceProvider serviceProvider, JsonObject? arguments, out Guid id,
        out DateTimeOffset expected, out SurveyInstrumentController? controller,
        out SurveyInstrumentModel? current, out JsonNode? error)
    {
        expected = default;
        controller = null;
        current = null;
        if (!McpToolArgumentHelpers.TryParseGuid(arguments, "id", out id, out error))
        {
            return false;
        }
        if (!DateTimeOffset.TryParse(arguments?["expectedModifiedUtc"]?.ToString(), out expected))
        {
            error = McpToolResponses.CreateValidationError(
                "Argument 'expectedModifiedUtc' must be an ISO 8601 date-time.");
            return false;
        }

        controller = SurveyInstrumentController(serviceProvider);
        ActionResult<SurveyInstrumentModel?> lookup = controller.GetSurveyInstrumentById(id);
        current = lookup.Value ?? (lookup.Result as ObjectResult)?.Value as SurveyInstrumentModel;
        if (current is null)
        {
            error = McpActionResultConverter.FromActionResult(lookup);
            return false;
        }
        if (current.LastModificationDate != expected)
        {
            error = McpToolResponses.CreateConflict("stale_write",
                "The survey instrument changed after it was read. Read it again and retry with its latest LastModificationDate.");
            return false;
        }

        error = null;
        return true;
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

    private static Task<JsonNode?> InvokeWithBodyResult<TModel, TResult>(JsonObject? arguments, string bodyName,
        CancellationToken cancellationToken, Func<TModel?, ActionResult<TResult>> action)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryDeserialize(arguments, bodyName, out TModel? data, out JsonNode? error))
            return Task.FromResult(error);
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
