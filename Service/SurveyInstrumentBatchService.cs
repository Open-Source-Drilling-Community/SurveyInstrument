using Microsoft.Data.Sqlite;
using OSDC.DotnetLibraries.Drilling.Surveying;
using OSDC.Drilling.SurveyInstrument.Model;
using OSDC.Drilling.SurveyInstrument.Service.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OSDC.Drilling.SurveyInstrument.Service;

internal enum SurveyInstrumentBatchFailureKind { None, InvalidRequest, NotFound, Conflict, StorageFailure }

internal sealed class SurveyInstrumentBatchExportOutcome
{
    public SurveyInstrumentBatchExportDocument? Document { get; init; }
    public SurveyInstrumentBatchErrorEnvelope? Error { get; init; }
    public SurveyInstrumentBatchFailureKind FailureKind { get; init; }
}

internal sealed class SurveyInstrumentBatchRestoreOutcome
{
    public SurveyInstrumentBatchRestoreResponse? Response { get; init; }
    public SurveyInstrumentBatchErrorEnvelope? Error { get; init; }
    public SurveyInstrumentBatchFailureKind FailureKind { get; init; }
}

internal sealed class SurveyInstrumentBatchService(SqlConnectionManager connections)
{
    public SurveyInstrumentBatchExportOutcome Export(SurveyInstrumentBatchExportRequest? request)
    {
        List<SurveyInstrumentBatchError> errors = ValidateExportRequest(request);
        if (errors.Count != 0) return ExportFailure(SurveyInstrumentBatchFailureKind.InvalidRequest,
            "invalid_batch_export_request", "The batch-export request is invalid.", errors);
        try
        {
            using SqliteConnection connection = connections.GetConnection()!;
            List<Model.SurveyInstrument> all = Read<Model.SurveyInstrument>(connection,
                "SurveyInstrumentTable", "SurveyInstrument");
            Dictionary<Guid, Model.SurveyInstrument> byId = Index(all, value => value.MetaInfo?.ID);
            List<Model.SurveyInstrument> selected = [];
            if (request!.Scope == SurveyInstrumentBatchExportScope.All)
                selected.AddRange(byId.OrderBy(pair => pair.Key).Select(pair => pair.Value));
            else
            {
                foreach ((Guid id, int index) in request.SurveyInstrumentIDs!.Select((id, index) => (id, index)))
                {
                    if (byId.TryGetValue(id, out Model.SurveyInstrument? value)) selected.Add(value);
                    else errors.Add(Error(index, "SurveyInstrumentIDs", "survey_instrument_not_found",
                        $"No stored survey instrument has UUID '{id}'."));
                }
                if (errors.Count != 0) return ExportFailure(SurveyInstrumentBatchFailureKind.NotFound,
                    "survey_instrument_not_found", "One or more selected survey instruments do not exist.", errors);
            }

            List<SurveyInstrumentIdentity> identities = Read<SurveyInstrumentIdentity>(connection,
                "SurveyInstrumentIdentityTable", "SurveyInstrumentIdentity");
            List<SurveyInstrumentFeatureCategory> categories = Read<SurveyInstrumentFeatureCategory>(connection,
                "SurveyInstrumentFeatureCategoryTable", "SurveyInstrumentFeatureCategory");
            List<ErrorSource> templates = Read<ErrorSource>(connection, "ErrorSourceTable", "ErrorSource");
            HashSet<Guid> identityIds = selected.SelectMany(value => value.SurveyInstrumentIdentityAssignments ?? [])
                .Where(value => value.IdentityID != null).Select(value => value.IdentityID!.Value).ToHashSet();
            HashSet<Guid> categoryIds = selected.SelectMany(value => value.SurveyInstrumentFeatureAssignments ?? [])
                .Where(value => value.FeatureCategoryID != null).Select(value => value.FeatureCategoryID!.Value).ToHashSet();
            HashSet<Guid> templateIds = selected.SelectMany(value => value.ErrorSourceList ?? [])
                .Where(value => value?.MetaInfo?.ID != Guid.Empty).Select(value => value.MetaInfo!.ID).ToHashSet();

            SurveyInstrumentBatchCatalogDependencies dependencies = new()
            {
                Identities = SelectDependencies(identities, identityIds, value => value.MetaInfo?.ID,
                    "CatalogDependencies.Identities", errors),
                FeatureCategories = SelectDependencies(categories, categoryIds, value => value.MetaInfo?.ID,
                    "CatalogDependencies.FeatureCategories", errors),
                ErrorSourceTemplates = request.Scope == SurveyInstrumentBatchExportScope.All
                    ? templates.OrderBy(value => value.MetaInfo!.ID).ToList()
                    : SelectDependencies(templates, templateIds, value => value.MetaInfo?.ID,
                        "CatalogDependencies.ErrorSourceTemplates", errors, false)
            };
            if (errors.Count != 0) return ExportFailure(SurveyInstrumentBatchFailureKind.StorageFailure,
                "export_dependency_missing", "A referenced catalog dependency is missing.", errors);
            return new SurveyInstrumentBatchExportOutcome
            {
                Document = new SurveyInstrumentBatchExportDocument
                {
                    ExportedAtUtc = DateTimeOffset.UtcNow,
                    CatalogDependencies = dependencies,
                    SurveyInstruments = selected
                }
            };
        }
        catch (Exception ex) when (ex is SqliteException or JsonException or InvalidOperationException)
        {
            return ExportFailure(SurveyInstrumentBatchFailureKind.StorageFailure, "batch_export_failed",
                "The backup snapshot could not be read.", [Error(null, "Document", "storage_failure", ex.Message)]);
        }
    }

    public SurveyInstrumentBatchRestoreOutcome Restore(SurveyInstrumentBatchRestoreRequest? request)
    {
        List<SurveyInstrumentBatchError> errors = ValidateRestoreRequest(request);
        if (errors.Count != 0) return RestoreFailure(SurveyInstrumentBatchFailureKind.InvalidRequest,
            "invalid_batch_restore_request", "The batch-restore request is invalid. No changes were made.", errors);
        try
        {
            using SqliteConnection connection = connections.GetConnection()!;
            SurveyInstrumentBatchExportDocument document = request!.Document!;
            var dependencies = document.CatalogDependencies;
            ValidateUnique(dependencies.Identities, value => value.MetaInfo?.ID, "CatalogDependencies.Identities", errors);
            ValidateUnique(dependencies.FeatureCategories, value => value.MetaInfo?.ID, "CatalogDependencies.FeatureCategories", errors);
            ValidateUnique(dependencies.ErrorSourceTemplates, value => value.MetaInfo?.ID, "CatalogDependencies.ErrorSourceTemplates", errors);
            ValidateUnique(document.SurveyInstruments, value => value.MetaInfo?.ID, "SurveyInstruments", errors);
            foreach ((SurveyInstrumentFeatureCategory category, int index) in dependencies.FeatureCategories.Select((value, index) => (value, index)))
            {
                if (category.Options == null || category.Options.Any(option => option.ID == Guid.Empty) ||
                    category.Options.Select(option => option.ID).Distinct().Count() != category.Options.Count)
                    errors.Add(Error(index, "CatalogDependencies.FeatureCategories.Options", "invalid_options",
                        "Feature-option UUIDs must be non-empty and unique within a category."));
            }

            Dictionary<Guid, SurveyInstrumentIdentity> localIdentities = Index(
                Read<SurveyInstrumentIdentity>(connection, "SurveyInstrumentIdentityTable", "SurveyInstrumentIdentity"), value => value.MetaInfo?.ID);
            Dictionary<Guid, SurveyInstrumentFeatureCategory> localCategories = Index(
                Read<SurveyInstrumentFeatureCategory>(connection, "SurveyInstrumentFeatureCategoryTable", "SurveyInstrumentFeatureCategory"), value => value.MetaInfo?.ID);
            Dictionary<Guid, ErrorSource> localTemplates = Index(
                Read<ErrorSource>(connection, "ErrorSourceTable", "ErrorSource"), value => value.MetaInfo?.ID);
            Dictionary<Guid, Model.SurveyInstrument> localInstruments = Index(
                Read<Model.SurveyInstrument>(connection, "SurveyInstrumentTable", "SurveyInstrument"), value => value.MetaInfo?.ID);

            CheckCatalogConflicts(dependencies.Identities, localIdentities, value => value.MetaInfo!.ID,
                "CatalogDependencies.Identities", errors);
            CheckCatalogConflicts(dependencies.FeatureCategories, localCategories, value => value.MetaInfo!.ID,
                "CatalogDependencies.FeatureCategories", errors);
            CheckCatalogConflicts(dependencies.ErrorSourceTemplates, localTemplates, value => value.MetaInfo!.ID,
                "CatalogDependencies.ErrorSourceTemplates", errors);

            Dictionary<Guid, SurveyInstrumentIdentity> finalIdentities = localIdentities.Concat(
                dependencies.Identities.Where(value => !localIdentities.ContainsKey(value.MetaInfo!.ID))
                    .Select(value => new KeyValuePair<Guid, SurveyInstrumentIdentity>(value.MetaInfo!.ID, value)))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Dictionary<Guid, SurveyInstrumentFeatureCategory> finalCategories = localCategories.Concat(
                dependencies.FeatureCategories.Where(value => !localCategories.ContainsKey(value.MetaInfo!.ID))
                    .Select(value => new KeyValuePair<Guid, SurveyInstrumentFeatureCategory>(value.MetaInfo!.ID, value)))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            for (int index = 0; index < document.SurveyInstruments.Count; index++)
            {
                Model.SurveyInstrument instrument = document.SurveyInstruments[index];
                ValidateInstrument(instrument, index, finalIdentities, finalCategories, errors);
                if (instrument.MetaInfo != null && localInstruments.ContainsKey(instrument.MetaInfo.ID) &&
                    request.ConflictPolicy == SurveyInstrumentBatchRestoreConflictPolicy.FailIfExists)
                    errors.Add(Error(index, "SurveyInstruments.MetaInfo.ID", "survey_instrument_restore_conflict",
                        $"Survey instrument '{instrument.MetaInfo.ID}' already exists."));
            }
            if (errors.Count != 0) return RestoreFailure(SurveyInstrumentBatchFailureKind.Conflict,
                "batch_restore_conflict", "The backup conflicts with local data. No changes were made.", errors);

            int createdCatalogs = 0, created = 0, replaced = 0;
            using SqliteTransaction transaction = connection.BeginTransaction();
            foreach (SurveyInstrumentIdentity value in dependencies.Identities.Where(value => !localIdentities.ContainsKey(value.MetaInfo!.ID)))
            { WriteIdentity(connection, transaction, value); createdCatalogs++; }
            foreach (SurveyInstrumentFeatureCategory value in dependencies.FeatureCategories.Where(value => !localCategories.ContainsKey(value.MetaInfo!.ID)))
            { WriteCategory(connection, transaction, value); createdCatalogs++; }
            foreach (ErrorSource value in dependencies.ErrorSourceTemplates.Where(value => !localTemplates.ContainsKey(value.MetaInfo!.ID)))
            { WriteErrorSource(connection, transaction, value); createdCatalogs++; }
            foreach (Model.SurveyInstrument value in document.SurveyInstruments)
            {
                bool exists = localInstruments.ContainsKey(value.MetaInfo!.ID);
                WriteInstrument(connection, transaction, value, exists);
                if (exists) replaced++; else created++;
            }
            transaction.Commit();
            return new SurveyInstrumentBatchRestoreOutcome
            {
                Response = new SurveyInstrumentBatchRestoreResponse
                {
                    RestoredAtUtc = DateTimeOffset.UtcNow, CreatedCount = created, ReplacedCount = replaced,
                    CreatedCatalogDefinitionCount = createdCatalogs,
                    SurveyInstrumentIDs = document.SurveyInstruments.Select(value => value.MetaInfo!.ID).ToList()
                }
            };
        }
        catch (Exception ex) when (ex is SqliteException or JsonException or InvalidOperationException)
        {
            return RestoreFailure(SurveyInstrumentBatchFailureKind.StorageFailure, "batch_restore_failed",
                "The backup could not be restored. No changes were committed.",
                [Error(null, "Document", "storage_failure", ex.Message)]);
        }
    }

    private static void ValidateInstrument(Model.SurveyInstrument value, int index,
        IReadOnlyDictionary<Guid, SurveyInstrumentIdentity> identities,
        IReadOnlyDictionary<Guid, SurveyInstrumentFeatureCategory> categories, List<SurveyInstrumentBatchError> errors)
    {
        if (value.MetaInfo == null || value.MetaInfo.ID == Guid.Empty || !SurveyInstrumentManager.ValidateModelSemantics(value))
            errors.Add(Error(index, "SurveyInstruments", "invalid_survey_instrument", "The instrument UUID or model-family fields are invalid."));
        if ((value.ErrorSourceList ?? []).Any(source => source?.MetaInfo?.ID is not Guid id || id == Guid.Empty))
            errors.Add(Error(index, "SurveyInstruments.ErrorSourceList.MetaInfo.ID", "invalid_error_source",
                "Every embedded error-source snapshot requires a non-empty UUID."));
        if ((value.SurveyInstrumentIdentityAssignments ?? []).Any(assignment => assignment.ID == Guid.Empty) ||
            (value.SurveyInstrumentIdentityAssignments ?? []).Select(assignment => assignment.ID).Distinct().Count() !=
            (value.SurveyInstrumentIdentityAssignments ?? []).Count)
            errors.Add(Error(index, "SurveyInstrumentIdentityAssignments.ID", "invalid_assignment_ids",
                "Identity-assignment UUIDs must be non-empty and unique."));
        if ((value.SurveyInstrumentFeatureAssignments ?? []).Any(assignment => assignment.ID == Guid.Empty) ||
            (value.SurveyInstrumentFeatureAssignments ?? []).Select(assignment => assignment.ID).Distinct().Count() !=
            (value.SurveyInstrumentFeatureAssignments ?? []).Count)
            errors.Add(Error(index, "SurveyInstrumentFeatureAssignments.ID", "invalid_assignment_ids",
                "Feature-assignment UUIDs must be non-empty and unique."));
        foreach (SurveyInstrumentIdentityAssignment assignment in value.SurveyInstrumentIdentityAssignments ?? [])
            if (assignment.IdentityID is not Guid id || !identities.ContainsKey(id))
                errors.Add(Error(index, "SurveyInstrumentIdentityAssignments.IdentityID", "missing_identity", "An identity dependency is missing."));
        foreach (SurveyInstrumentFeatureAssignment assignment in value.SurveyInstrumentFeatureAssignments ?? [])
        {
            if (assignment.FeatureCategoryID is not Guid categoryId || !categories.TryGetValue(categoryId, out SurveyInstrumentFeatureCategory? category) ||
                assignment.FeatureOptionID is not Guid optionId || category.Options?.Any(option => option.ID == optionId) != true)
                errors.Add(Error(index, "SurveyInstrumentFeatureAssignments", "missing_feature_option", "A feature-category or option dependency is missing."));
            else
            {
                if (!category.HasValidityPeriod && (assignment.FromDate != null || assignment.ToDate != null))
                    errors.Add(Error(index, "SurveyInstrumentFeatureAssignments", "validity_not_supported", "This category does not support validity dates."));
                if (assignment.FromDate > assignment.ToDate)
                    errors.Add(Error(index, "SurveyInstrumentFeatureAssignments", "invalid_validity_period", "FromDate must not follow ToDate."));
            }
        }
        foreach (SurveyInstrumentFeatureCategory category in categories.Values.Where(category => category.IsExclusive))
        {
            List<SurveyInstrumentFeatureAssignment> assigned = (value.SurveyInstrumentFeatureAssignments ?? [])
                .Where(assignment => assignment.FeatureCategoryID == category.MetaInfo!.ID).ToList();
            for (int left = 0; left < assigned.Count; left++)
                for (int right = left + 1; right < assigned.Count; right++)
                    if (PeriodsOverlap(assigned[left], assigned[right]))
                        errors.Add(Error(index, "SurveyInstrumentFeatureAssignments", "exclusive_assignments_overlap",
                            $"Assignments in exclusive category '{category.MetaInfo!.ID}' overlap."));
        }
    }

    private static bool PeriodsOverlap(SurveyInstrumentFeatureAssignment left, SurveyInstrumentFeatureAssignment right) =>
        (left.ToDate == null || right.FromDate == null || left.ToDate >= right.FromDate) &&
        (right.ToDate == null || left.FromDate == null || right.ToDate >= left.FromDate);

    private static List<SurveyInstrumentBatchError> ValidateExportRequest(SurveyInstrumentBatchExportRequest? request)
    {
        if (request == null) return [Error(null, "Request", "required", "A request is required.")];
        List<SurveyInstrumentBatchError> errors = [];
        if (request.Scope == SurveyInstrumentBatchExportScope.All)
        {
            if (request.SurveyInstrumentIDs is { Count: > 0 }) errors.Add(Error(null, "SurveyInstrumentIDs", "forbidden", "IDs must be omitted for an All export."));
        }
        else if (request.Scope == SurveyInstrumentBatchExportScope.Selected)
        {
            if (request.SurveyInstrumentIDs is not { Count: > 0 }) errors.Add(Error(null, "SurveyInstrumentIDs", "required", "Selected export requires IDs."));
            else if (request.SurveyInstrumentIDs.Any(id => id == Guid.Empty) || request.SurveyInstrumentIDs.Distinct().Count() != request.SurveyInstrumentIDs.Count)
                errors.Add(Error(null, "SurveyInstrumentIDs", "invalid_ids", "IDs must be non-empty and unique."));
        }
        else errors.Add(Error(null, "Scope", "invalid_scope", "Scope must be All or Selected."));
        return errors;
    }

    private static List<SurveyInstrumentBatchError> ValidateRestoreRequest(SurveyInstrumentBatchRestoreRequest? request)
    {
        List<SurveyInstrumentBatchError> errors = [];
        if (request?.Document == null) return [Error(null, "Document", "required", "A backup document is required.")];
        if (request.ConflictPolicy is not SurveyInstrumentBatchRestoreConflictPolicy.FailIfExists and not SurveyInstrumentBatchRestoreConflictPolicy.ReplaceExisting)
            errors.Add(Error(null, "ConflictPolicy", "invalid_policy", "ConflictPolicy must be FailIfExists or ReplaceExisting."));
        if (request.Document.FormatIdentifier != SurveyInstrumentBatchExportDocument.CurrentFormatIdentifier)
            errors.Add(Error(null, "Document.FormatIdentifier", "unsupported_format", "The backup format identifier is unsupported."));
        if (request.Document.SchemaVersion != SurveyInstrumentBatchExportDocument.CurrentSchemaVersion)
            errors.Add(Error(null, "Document.SchemaVersion", "unsupported_schema_version", "The backup schema version is unsupported."));
        if (request.Document.CatalogDependencies == null) errors.Add(Error(null, "Document.CatalogDependencies", "required", "CatalogDependencies is required."));
        if (request.Document.SurveyInstruments == null) errors.Add(Error(null, "Document.SurveyInstruments", "required", "SurveyInstruments is required."));
        return errors;
    }

    private static List<T> Read<T>(SqliteConnection connection, string table, string column)
    {
        using SqliteCommand command = connection.CreateCommand(); command.CommandText = $"SELECT {column} FROM {table}";
        using SqliteDataReader reader = command.ExecuteReader(); List<T> result = [];
        while (reader.Read()) { T? value = JsonSerializer.Deserialize<T>(reader.GetString(0), JsonSettings.Options); if (value != null) result.Add(value); }
        return result;
    }

    private static Dictionary<Guid, T> Index<T>(IEnumerable<T> values, Func<T, Guid?> id) =>
        values.Where(value => id(value) is Guid key && key != Guid.Empty).ToDictionary(value => id(value)!.Value);

    private static List<T> SelectDependencies<T>(IEnumerable<T> values, HashSet<Guid> ids, Func<T, Guid?> id,
        string property, List<SurveyInstrumentBatchError> errors, bool required = true)
    {
        Dictionary<Guid, T> index = Index(values, id); List<T> result = [];
        foreach (Guid key in ids.Order())
            if (index.TryGetValue(key, out T? value)) result.Add(value);
            else if (required) errors.Add(Error(null, property, "referenced_definition_missing", $"Referenced definition '{key}' is missing."));
        return result;
    }

    private static void ValidateUnique<T>(IEnumerable<T> values, Func<T, Guid?> id, string property, List<SurveyInstrumentBatchError> errors)
    {
        HashSet<Guid> seen = [];
        foreach ((T value, int index) in values.Select((value, index) => (value, index)))
            if (id(value) is not Guid key || key == Guid.Empty) errors.Add(Error(index, property, "empty_uuid", "A non-empty UUID is required."));
            else if (!seen.Add(key)) errors.Add(Error(index, property, "duplicate_uuid", $"UUID '{key}' occurs more than once."));
    }

    private static void CheckCatalogConflicts<T>(IEnumerable<T> incoming, IReadOnlyDictionary<Guid, T> local,
        Func<T, Guid> id, string property, List<SurveyInstrumentBatchError> errors)
    {
        foreach (T value in incoming)
            if (local.TryGetValue(id(value), out T? existing) && !SemanticJson(existing).Equals(SemanticJson(value)))
                errors.Add(Error(null, property, "catalog_definition_conflict", $"Catalog UUID '{id(value)}' has different local content."));
    }

    private static string SemanticJson<T>(T value)
    {
        JsonObject node = JsonSerializer.SerializeToNode(value, JsonSettings.Options)!.AsObject();
        node.Remove("CreationDate"); node.Remove("LastModificationDate");
        return node.ToJsonString(JsonSettings.Options);
    }

    private static void WriteErrorSource(SqliteConnection c, SqliteTransaction t, ErrorSource value)
    {
        using SqliteCommand command = c.CreateCommand(); command.Transaction = t;
        command.CommandText = "INSERT INTO ErrorSourceTable(ID,MetaInfo,ErrorSource) VALUES($id,$meta,$doc)";
        Add(command, "$id", value.MetaInfo!.ID.ToString()); Add(command, "$meta", JsonSerializer.Serialize(value.MetaInfo, JsonSettings.Options)); Add(command, "$doc", JsonSerializer.Serialize(value, JsonSettings.Options)); command.ExecuteNonQuery();
    }

    private static void WriteIdentity(SqliteConnection c, SqliteTransaction t, SurveyInstrumentIdentity value)
    {
        using SqliteCommand command = c.CreateCommand(); command.Transaction = t;
        command.CommandText = "INSERT INTO SurveyInstrumentIdentityTable(ID,MetaInfo,Name,CreationDate,LastModificationDate,SurveyInstrumentIdentity) VALUES($id,$meta,$name,$created,$modified,$doc)";
        AddCommon(command, value.MetaInfo!, value.Name, value.CreationDate, value.LastModificationDate, value); command.ExecuteNonQuery();
    }

    private static void WriteCategory(SqliteConnection c, SqliteTransaction t, SurveyInstrumentFeatureCategory value)
    {
        using SqliteCommand command = c.CreateCommand(); command.Transaction = t;
        command.CommandText = "INSERT INTO SurveyInstrumentFeatureCategoryTable(ID,MetaInfo,Name,IsExclusive,HasValidityPeriod,CreationDate,LastModificationDate,SurveyInstrumentFeatureCategory) VALUES($id,$meta,$name,$exclusive,$validity,$created,$modified,$doc)";
        AddCommon(command, value.MetaInfo!, value.Name, value.CreationDate, value.LastModificationDate, value);
        Add(command, "$exclusive", value.IsExclusive ? 1 : 0); Add(command, "$validity", value.HasValidityPeriod ? 1 : 0); command.ExecuteNonQuery();
    }

    private static void WriteInstrument(SqliteConnection c, SqliteTransaction t, Model.SurveyInstrument value, bool replace)
    {
        using SqliteCommand command = c.CreateCommand(); command.Transaction = t;
        command.CommandText = (replace ? "INSERT OR REPLACE" : "INSERT") + " INTO SurveyInstrumentTable(ID,MetaInfo,Name,Description,CreationDate,LastModificationDate,SurveyInstrument) VALUES($id,$meta,$name,$description,$created,$modified,$doc)";
        AddCommon(command, value.MetaInfo!, value.Name, value.CreationDate, value.LastModificationDate, value);
        Add(command, "$description", value.Description); command.ExecuteNonQuery();
    }

    private static void AddCommon<T>(SqliteCommand command, OSDC.DotnetLibraries.General.DataManagement.MetaInfo meta,
        string? name, DateTimeOffset? created, DateTimeOffset? modified, T document)
    {
        Add(command, "$id", meta.ID.ToString()); Add(command, "$meta", JsonSerializer.Serialize(meta, JsonSettings.Options));
        Add(command, "$name", name); Add(command, "$created", created?.ToString("O")); Add(command, "$modified", modified?.ToString("O"));
        Add(command, "$doc", JsonSerializer.Serialize(document, JsonSettings.Options));
    }

    private static void Add(SqliteCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    private static SurveyInstrumentBatchError Error(int? index, string property, string code, string message) => new() { PositionIndex = index, Property = property, Code = code, Message = message };
    private static SurveyInstrumentBatchExportOutcome ExportFailure(SurveyInstrumentBatchFailureKind kind, string code, string message, List<SurveyInstrumentBatchError> errors) => new() { FailureKind = kind, Error = new() { Error = code, Message = message, Errors = errors } };
    private static SurveyInstrumentBatchRestoreOutcome RestoreFailure(SurveyInstrumentBatchFailureKind kind, string code, string message, List<SurveyInstrumentBatchError> errors) => new() { FailureKind = kind, Error = new() { Error = code, Message = message, Errors = errors } };
}
