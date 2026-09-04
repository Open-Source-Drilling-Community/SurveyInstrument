using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using OSDC.Drilling.SurveyInstrument.Service.Controllers;
using OSDC.Drilling.SurveyInstrument.Service.Mcp;
using OSDC.Drilling.SurveyInstrument.Service.Mcp.Tools;
using NUnit.Framework;

namespace OSDC.Drilling.SurveyInstrument.ServiceTest;

[TestFixture]
public sealed class McpToolRegistrationTests
{
    private static readonly IReadOnlyDictionary<string, string> EndpointToolMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GetAllSurveyInstrumentId"] = "survey_instrument_get_all_ids",
            ["GetAllSurveyInstrumentMetaInfo"] = "survey_instrument_get_all_meta_info",
            ["GetSurveyInstrumentById"] = "survey_instrument_get_by_id",
            ["GetAllSurveyInstrumentLight"] = "survey_instrument_get_all_light",
            ["GetAllSurveyInstrument"] = "survey_instrument_get_all",
            ["PostSurveyInstrument"] = "survey_instrument_create",
            ["PutSurveyInstrumentById"] = "survey_instrument_update_by_id",
            ["DeleteSurveyInstrumentById"] = "survey_instrument_delete_by_id",
            ["BatchExportSurveyInstruments"] = "survey_instrument_batch_export",
            ["BatchRestoreSurveyInstruments"] = "survey_instrument_batch_restore",
            ["GetAllErrorSourceId"] = "error_source_get_all_ids",
            ["GetAllErrorSourceMetaInfo"] = "error_source_get_all_meta_info",
            ["GetErrorSourceById"] = "error_source_get_by_id",
            ["GetAllErrorSource"] = "error_source_get_all",
            ["PostErrorSource"] = "error_source_create",
            ["PutErrorSourceById"] = "error_source_update_by_id",
            ["DeleteErrorSourceById"] = "error_source_delete_by_id",
            ["IdentityGetAll"] = "survey_instrument_identity_get_all",
            ["IdentityGetById"] = "survey_instrument_identity_get_by_id",
            ["IdentityCreate"] = "survey_instrument_identity_create",
            ["IdentityUpdate"] = "survey_instrument_identity_update_by_id",
            ["IdentityDelete"] = "survey_instrument_identity_delete_by_id",
            ["FeatureGetAll"] = "survey_instrument_feature_category_get_all",
            ["FeatureGetById"] = "survey_instrument_feature_category_get_by_id",
            ["FeatureCreate"] = "survey_instrument_feature_category_create",
            ["FeatureUpdate"] = "survey_instrument_feature_category_update_by_id",
            ["FeatureDelete"] = "survey_instrument_feature_category_delete_by_id"
        };

    private ServiceProvider _provider = default!;
    private IReadOnlyDictionary<string, IMcpTool> _tools = default!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLegacyMcpTool<PingMcpTool>();
        services.AddSurveyInstrumentRestMcpTools();

        _provider = services.BuildServiceProvider();
        _tools = _provider.GetServices<IMcpTool>()
            .ToDictionary(tool => tool.Name, StringComparer.Ordinal);
    }

    [TearDown]
    public void TearDown() => _provider.Dispose();

    [Test]
    public void Every_non_statistics_controller_endpoint_has_a_registered_tool()
    {
        var endpointMethods = new[]
            {
                typeof(SurveyInstrumentController),
                typeof(ErrorSourceController)
            }
            .SelectMany(type => type.GetMethods())
            .Where(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true).Length > 0)
            .Select(method => method.Name)
            .ToArray();

        Assert.That(endpointMethods, Is.EquivalentTo(EndpointToolMap.Keys.Take(17)));
        Assert.That(_tools.Keys, Is.EquivalentTo(EndpointToolMap.Values
            .Append("survey_instrument_patch_by_id")
            .Append("survey_instrument_error_source_mutate")
            .Append("survey_instrument_check_error_source_drift")
            .Append("survey_instrument_validate_catalog_references")
            .Append("survey_instrument_audit_catalog_references")
            .Append("ping")));
    }

    [Test]
    public void Registered_tools_have_unique_names_and_descriptions()
    {
        Assert.That(_tools.Count, Is.EqualTo(EndpointToolMap.Count + 6));
        Assert.That(_tools.Values.Select(tool => tool.Name), Is.Unique);
        Assert.That(_tools.Values.All(tool => !string.IsNullOrWhiteSpace(tool.Description)), Is.True);
    }

    [Test]
    public void Domain_tools_have_detailed_descriptions_and_explicit_object_schemas()
    {
        var domainTools = _tools.Values.Where(tool => tool.Name != "ping").ToArray();

        Assert.That(domainTools.All(tool => tool.Description.Length >= 150), Is.True);
        Assert.That(domainTools.All(tool => tool.InputSchema is JsonObject), Is.True);
        Assert.That(domainTools.All(tool => tool.InputSchema?["type"]?.GetValue<string>() == "object"), Is.True);
        Assert.That(domainTools.All(tool => tool.OutputSchema is JsonObject), Is.True);
        Assert.That(domainTools.All(tool => tool.OutputSchema["type"]?.GetValue<string>() == "object"), Is.True);
    }

    [Test]
    public void Survey_instrument_write_schema_documents_models_nested_error_sources_and_si_units()
    {
        string schema = _tools["survey_instrument_create"].InputSchema!.ToJsonString();

        Assert.That(schema, Does.Contain("MWD_WolffDeWardt"));
        Assert.That(schema, Does.Contain("Gyro_ISCWSA"));
        Assert.That(schema, Does.Contain("ErrorSourceList"));
        Assert.That(schema, Does.Contain("MagnitudeQuantity"));
        Assert.That(schema, Does.Contain("radians"));
        Assert.That(schema, Does.Contain("metres per second squared"));
        Assert.That(schema, Does.Contain("tesla"));
        Assert.That(schema, Does.Contain("SurveyInstrumentIdentityAssignments"));
        Assert.That(schema, Does.Contain("SurveyInstrumentFeatureAssignments"));
        Assert.That(schema, Does.Contain("\"oneOf\""));
        Assert.That(schema, Does.Contain("\"not\""));
        Assert.That(schema, Does.Contain("\"anyOf\""));
        Assert.That(schema, Does.Contain("Authoritative error-source snapshots"));
        Assert.That(schema, Does.Contain("later catalog updates do not propagate").IgnoreCase);
    }

    [Test]
    public void Core_writes_require_optimistic_concurrency_and_patch_is_narrow()
    {
        string update = _tools["survey_instrument_update_by_id"].InputSchema.ToJsonString();
        JsonNode patchSchema = _tools["survey_instrument_patch_by_id"].InputSchema;
        string patch = patchSchema.ToJsonString();
        string delete = _tools["survey_instrument_delete_by_id"].InputSchema.ToJsonString();
        JsonObject patchFields = (JsonObject)patchSchema["properties"]!["patch"]!["properties"]!;

        Assert.Multiple(() =>
        {
            Assert.That(update, Does.Contain("expectedModifiedUtc"));
            Assert.That(delete, Does.Contain("expectedModifiedUtc"));
            Assert.That(patch, Does.Contain("expectedModifiedUtc"));
            Assert.That(patch, Does.Contain("arrays are replaced as a whole"));
            Assert.That(patchFields.ContainsKey("MetaInfo"), Is.False);
            Assert.That(patchFields.ContainsKey("CreationDate"), Is.False);
            Assert.That(patchFields.ContainsKey("LastModificationDate"), Is.False);
            Assert.That(_tools["survey_instrument_patch_by_id"].Behavior.IdempotentHint, Is.True);
        });
    }

    [Test]
    public void Error_source_snapshot_mutation_is_granular_guarded_and_schema_discriminated()
    {
        IMcpTool mutation = _tools["survey_instrument_error_source_mutate"];
        string input = mutation.InputSchema.ToJsonString();

        Assert.Multiple(() =>
        {
            Assert.That(input, Does.Contain("expectedModifiedUtc"));
            Assert.That(input, Does.Contain("\"const\":\"add\""));
            Assert.That(input, Does.Contain("\"const\":\"replace\""));
            Assert.That(input, Does.Contain("\"const\":\"remove\""));
            Assert.That(input, Does.Contain("errorSourceId"));
            Assert.That(mutation.Behavior.IdempotentHint, Is.True);
            Assert.That(mutation.OutputSchema.ToJsonString(), Does.Contain("ErrorSourceList"));
        });
    }

    [Test]
    public void Feature_catalog_write_schema_documents_exclusivity_validity_and_options()
    {
        string schema = _tools["survey_instrument_feature_category_update_by_id"].InputSchema!.ToJsonString();

        Assert.That(schema, Does.Contain("IsExclusive"));
        Assert.That(schema, Does.Contain("HasValidityPeriod"));
        Assert.That(schema, Does.Contain("Options"));
        Assert.That(schema, Does.Contain("expectedModifiedUtc"));
    }

    [Test]
    public void Error_source_write_schema_documents_si_magnitude_and_inclination_interval()
    {
        string schema = _tools["error_source_update_by_id"].InputSchema!.ToJsonString();

        Assert.That(schema, Does.Contain("ErrorCode"));
        Assert.That(schema, Does.Contain("MagnitudeQuantity"));
        Assert.That(schema, Does.Contain("SI unit"));
        Assert.That(schema, Does.Contain("StartInclination"));
        Assert.That(schema, Does.Contain("radians"));
        Assert.That(schema, Does.Contain("must equal errorSource.MetaInfo.ID"));
        Assert.That(schema, Does.Contain("\"enum\""));
        Assert.That(schema, Does.Contain("DRFR"));
        Assert.That(schema, Does.Contain("XCLA"));
        Assert.That(schema, Does.Contain("expectedVersionToken"));
        Assert.That(schema, Does.Contain("^[0-9a-f]{64}$"));
        Assert.That(_tools["error_source_get_by_id"].OutputSchema.ToJsonString(), Does.Contain("versionToken"));
        Assert.That(_tools["error_source_delete_by_id"].InputSchema.ToJsonString(), Does.Contain("expectedVersionToken"));
    }

    [Test]
    public void Batch_tools_publish_versioned_atomic_backup_contracts()
    {
        string export = _tools["survey_instrument_batch_export"].OutputSchema.ToJsonString();
        string restore = _tools["survey_instrument_batch_restore"].InputSchema.ToJsonString();

        Assert.Multiple(() =>
        {
            Assert.That(export, Does.Contain("OSDC.Drilling.SurveyInstrument.BatchExport"));
            Assert.That(export, Does.Contain("ErrorSourceTemplates"));
            Assert.That(export, Does.Contain("FeatureCategories"));
            Assert.That(restore, Does.Contain("FailIfExists"));
            Assert.That(restore, Does.Contain("ReplaceExisting"));
            Assert.That(restore, Does.Contain("MapExisting"));
            Assert.That(restore, Does.Contain("MapOrCreateMissing"));
            Assert.That(_tools["survey_instrument_batch_export"].Behavior.ReadOnlyHint, Is.True);
            Assert.That(_tools["survey_instrument_batch_restore"].Behavior.DestructiveHint, Is.True);
        });
    }

    [Test]
    public void Drift_check_is_read_only_and_has_a_bounded_output_vocabulary()
    {
        IMcpTool tool = _tools["survey_instrument_check_error_source_drift"];
        string output = tool.OutputSchema.ToJsonString();
        Assert.Multiple(() =>
        {
            Assert.That(tool.Behavior.ReadOnlyHint, Is.True);
            Assert.That(output, Does.Contain("in_sync"));
            Assert.That(output, Does.Contain("drifted"));
            Assert.That(output, Does.Contain("catalog_missing"));
            Assert.That(output, Does.Contain("HasDrift"));
        });
    }

    [Test]
    public void Catalog_reference_diagnostics_are_read_only_bounded_and_explicit()
    {
        IMcpTool validate = _tools["survey_instrument_validate_catalog_references"];
        IMcpTool audit = _tools["survey_instrument_audit_catalog_references"];
        string validationOutput = validate.OutputSchema.ToJsonString();
        string auditInput = audit.InputSchema.ToJsonString();

        Assert.Multiple(() =>
        {
            Assert.That(validate.Behavior.ReadOnlyHint, Is.True);
            Assert.That(audit.Behavior.ReadOnlyHint, Is.True);
            Assert.That(validationOutput, Does.Contain("identity_missing"));
            Assert.That(validationOutput, Does.Contain("feature_category_missing"));
            Assert.That(validationOutput, Does.Contain("feature_option_missing"));
            Assert.That(auditInput, Does.Contain("\"maximum\":100"));
            Assert.That(audit.OutputSchema.ToJsonString(), Does.Contain("HasMore"));
        });
    }

    [Test]
    public void Error_source_update_reports_frozen_snapshot_impact()
    {
        IMcpTool update = _tools["error_source_update_by_id"];
        string output = update.OutputSchema.ToJsonString();

        Assert.Multiple(() =>
        {
            Assert.That(update.Description, Does.Contain("frozen snapshots"));
            Assert.That(output, Does.Contain("AffectedSnapshotCount"));
            Assert.That(output, Does.Contain("AffectedSurveyInstrumentIDs"));
            Assert.That(output, Does.Contain("Warning"));
        });
    }

    [Test]
    public void Usage_statistics_are_not_exposed()
    {
        Assert.That(_tools.Keys, Has.None.Contains("usage_statistics"));
        Assert.That(_tools.Keys, Has.None.Contains("statistics"));
    }

    [Test]
    public void Protocol_tool_names_are_valid_and_unique()
    {
        var names = _provider.GetServices<McpServerTool>()
            .Select(tool => tool.ProtocolTool.Name)
            .ToArray();

        Assert.That(names, Has.Length.EqualTo(_tools.Count));
        Assert.That(names, Is.Unique);
        Assert.That(names.All(name => !name.Contains('.')), Is.True);
    }

    [Test]
    public void Protocol_contract_publishes_output_schemas_titles_and_safety_annotations()
    {
        McpServerTool[] protocolTools = _provider.GetServices<McpServerTool>().ToArray();
        IMcpTool read = _tools["survey_instrument_get_by_id"];
        IMcpTool update = _tools["survey_instrument_update_by_id"];
        IMcpTool delete = _tools["survey_instrument_delete_by_id"];

        Assert.Multiple(() =>
        {
            Assert.That(protocolTools.All(tool => tool.ProtocolTool.OutputSchema.HasValue), Is.True);
            Assert.That(protocolTools.All(tool => !string.IsNullOrWhiteSpace(tool.ProtocolTool.Title)), Is.True);
            Assert.That(protocolTools.All(tool => tool.ProtocolTool.Annotations != null), Is.True);
            Assert.That(read.Behavior.ReadOnlyHint, Is.True);
            Assert.That(read.Behavior.DestructiveHint, Is.False);
            Assert.That(update.Behavior.ReadOnlyHint, Is.False);
            Assert.That(update.Behavior.IdempotentHint, Is.True);
            Assert.That(delete.Behavior.DestructiveHint, Is.True);
            Assert.That(delete.Behavior.IdempotentHint, Is.True);
        });
    }

    [Test]
    public void Resource_output_schemas_describe_success_envelopes_and_complete_data()
    {
        string instrument = _tools["survey_instrument_get_by_id"].OutputSchema.ToJsonString();
        string errorSources = _tools["error_source_get_all"].OutputSchema.ToJsonString();
        string features = _tools["survey_instrument_feature_category_get_all"].OutputSchema.ToJsonString();

        Assert.Multiple(() =>
        {
            Assert.That(instrument, Does.Contain("\"status\""));
            Assert.That(instrument, Does.Contain("SurveyInstrumentFeatureAssignments"));
            Assert.That(instrument, Does.Contain("ErrorSourceList"));
            Assert.That(errorSources, Does.Contain("MagnitudeQuantity"));
            Assert.That(features, Does.Contain("HasValidityPeriod"));
            Assert.That(features, Does.Contain("Options"));
        });
    }

    [Test]
    public async Task Unexpected_top_level_arguments_are_rejected_before_invocation()
    {
        JsonObject? response = await _tools["survey_instrument_get_all_ids"]
            .InvokeAsync(new JsonObject { ["typo"] = true }, CancellationToken.None) as JsonObject;

        Assert.That(response?["status"]?.GetValue<int>(), Is.EqualTo(400));
        Assert.That(response?["error"]?.GetValue<string>(), Does.Contain("Unexpected argument"));
    }

    [TestCase("survey_instrument_get_by_id")]
    [TestCase("error_source_get_by_id")]
    public async Task Get_by_id_tools_require_an_id(string toolName)
    {
        var response = await _tools[toolName].InvokeAsync(new JsonObject(), CancellationToken.None) as JsonObject;

        Assert.That(response, Is.Not.Null);
        Assert.That(response!["status"]?.GetValue<int>(), Is.EqualTo(400));
        Assert.That(response["error"]?.GetValue<string>(), Does.Contain("id"));
    }

    [TestCase("survey_instrument_create")]
    [TestCase("error_source_create")]
    public async Task Create_tools_require_a_request_body(string toolName)
    {
        var response = await _tools[toolName].InvokeAsync(new JsonObject(), CancellationToken.None) as JsonObject;

        Assert.That(response, Is.Not.Null);
        Assert.That(response!["status"]?.GetValue<int>(), Is.EqualTo(400));
    }

    [TestCase("survey_instrument_update_by_id")]
    [TestCase("survey_instrument_patch_by_id")]
    [TestCase("survey_instrument_delete_by_id")]
    public async Task Core_writes_reject_a_missing_concurrency_token(string toolName)
    {
        JsonObject? response = await _tools[toolName].InvokeAsync(
            new JsonObject { ["id"] = Guid.NewGuid().ToString() }, CancellationToken.None) as JsonObject;

        Assert.That(response?["status"]?.GetValue<int>(), Is.EqualTo(400));
        Assert.That(response?["error"]?.GetValue<string>(), Does.Contain("expectedModifiedUtc"));
    }

    [TestCase("error_source_update_by_id")]
    [TestCase("error_source_delete_by_id")]
    public async Task Error_source_writes_reject_a_missing_version_token(string toolName)
    {
        JsonObject? response = await _tools[toolName].InvokeAsync(
            new JsonObject { ["id"] = Guid.NewGuid().ToString() }, CancellationToken.None) as JsonObject;

        Assert.That(response?["status"]?.GetValue<int>(), Is.EqualTo(400));
        Assert.That(response?["error"]?.GetValue<string>(), Does.Contain("expectedVersionToken"));
    }
}
