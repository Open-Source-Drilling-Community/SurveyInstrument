using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OSDC.Drilling.SurveyInstrument.Service.Mcp;
using OSDC.Drilling.SurveyInstrument.Service.Mcp.Tools;
using NUnit.Framework;

namespace OSDC.Drilling.SurveyInstrument.ServiceTest;

[TestFixture]
public sealed class McpServerHttpTests
{
    private const string McpEndpoint = "http://localhost:8080/surveyinstrument/api/mcp";

    private HttpClientTransport _transport = default!;
    private McpClient _client = default!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(McpEndpoint),
            TransportMode = HttpTransportMode.AutoDetect
        };
        _transport = new HttpClientTransport(transportOptions, NullLoggerFactory.Instance);
        _client = await McpClient.CreateAsync(
            _transport,
            new McpClientOptions
            {
                ClientInfo = new Implementation
                {
                    Name = "SurveyInstrumentServiceTest",
                    Version = "1.0.0"
                }
            },
            NullLoggerFactory.Instance,
            CancellationToken.None);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
        if (_transport is not null)
        {
            await _transport.DisposeAsync();
        }
    }

    [Test]
    public async Task Http_endpoint_publishes_every_registered_non_statistics_tool()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLegacyMcpTool<PingMcpTool>();
        services.AddSurveyInstrumentRestMcpTools();
        using var provider = services.BuildServiceProvider();

        var expectedNames = provider.GetServices<McpServerTool>()
            .Select(tool => tool.ProtocolTool.Name)
            .ToArray();
        var remoteTools = await _client.ListToolsAsync(cancellationToken: CancellationToken.None);
        var remoteNames = remoteTools.Select(tool => tool.Name).ToArray();

        Assert.That(remoteNames, Is.EquivalentTo(expectedNames));
        Assert.That(remoteNames, Has.None.Contains("statistics"));
    }

    [Test]
    public async Task Ping_can_be_invoked_over_http()
    {
        var result = await _client.CallToolAsync(
            "ping",
            new Dictionary<string, object?>(),
            cancellationToken: CancellationToken.None);

        Assert.That(result.StructuredContent, Is.InstanceOf<JsonObject>());
        var payload = (JsonObject)result.StructuredContent!;
        Assert.That(payload["message"]?.GetValue<string>(), Is.EqualTo("pong"));
        Assert.That(result.Content.OfType<TextContentBlock>().Single().Text, Does.Contain("pong"));
    }

    [Test]
    public async Task Missing_resource_is_returned_as_a_stable_mcp_error()
    {
        CallToolResult result = await _client.CallToolAsync(
            "survey_instrument_get_by_id",
            new Dictionary<string, object?> { ["id"] = Guid.NewGuid().ToString() },
            cancellationToken: CancellationToken.None);

        Assert.That(result.IsError, Is.True);
        JsonObject error = JsonNode.Parse(result.Content.OfType<TextContentBlock>().Single().Text)!.AsObject();
        Assert.Multiple(() =>
        {
            Assert.That(error["error"]?.GetValue<string>(), Is.EqualTo("not_found"));
            Assert.That(error["message"]?.GetValue<string>(), Is.Not.Empty);
            Assert.That(error["errors"], Is.InstanceOf<JsonArray>());
            Assert.That(error.ToJsonString(), Does.Not.Contain("Exception"));
        });
    }

    [Test]
    public async Task Invalid_model_family_fields_and_unknown_error_codes_are_rejected()
    {
        CallToolResult invalidFamily = await _client.CallToolAsync("survey_instrument_create",
            new Dictionary<string, object?>
            {
                ["surveyInstrument"] = new JsonObject
                {
                    ["MetaInfo"] = new JsonObject { ["ID"] = Guid.NewGuid().ToString() },
                    ["ModelType"] = "MWD_WolffDeWardt",
                    ["ErrorSourceList"] = new JsonArray(new JsonObject
                    {
                        ["MetaInfo"] = new JsonObject { ["ID"] = Guid.NewGuid().ToString() },
                        ["ErrorCode"] = "DRFR"
                    })
                }
            }, cancellationToken: CancellationToken.None);
        Assert.That(invalidFamily.IsError, Is.True);
        Assert.That(JsonNode.Parse(invalidFamily.Content.OfType<TextContentBlock>().Single().Text)!["error"]?.GetValue<string>(),
            Is.EqualTo("invalid_model_family"));

        CallToolResult invalidCode = await _client.CallToolAsync("error_source_create",
            new Dictionary<string, object?>
            {
                ["errorSource"] = new JsonObject
                {
                    ["MetaInfo"] = new JsonObject { ["ID"] = Guid.NewGuid().ToString() },
                    ["ErrorCode"] = "NOT_A_REAL_ERROR_CODE"
                }
            }, cancellationToken: CancellationToken.None);
        Assert.That(invalidCode.IsError, Is.True);
        Assert.That(JsonNode.Parse(invalidCode.Content.OfType<TextContentBlock>().Single().Text)!["error"]?.GetValue<string>(),
            Is.EqualTo("validation_failed"));
    }

    [Test]
    public async Task Patch_updates_selected_fields_and_rejects_a_stale_retry()
    {
        Guid id = Guid.NewGuid();
        string initialTimestamp = DateTimeOffset.UtcNow.ToString("O");
        var instrument = new JsonObject
        {
            ["MetaInfo"] = new JsonObject { ["ID"] = id.ToString() },
            ["Name"] = "MCP concurrency test",
            ["Description"] = "original",
            ["CreationDate"] = initialTimestamp,
            ["LastModificationDate"] = initialTimestamp,
            ["ModelType"] = "MWD_WolffDeWardt"
        };

        CallToolResult created = await _client.CallToolAsync(
            "survey_instrument_create",
            new Dictionary<string, object?> { ["surveyInstrument"] = instrument },
            cancellationToken: CancellationToken.None);
        Assert.That(created.IsError, Is.Not.True);

        try
        {
            JsonObject first = await GetInstrument(id);
            string firstToken = first["LastModificationDate"]!.GetValue<string>();

            CallToolResult patched = await _client.CallToolAsync(
                "survey_instrument_patch_by_id",
                new Dictionary<string, object?>
                {
                    ["id"] = id.ToString(),
                    ["expectedModifiedUtc"] = firstToken,
                    ["patch"] = new JsonObject { ["Description"] = "patched" }
                }, cancellationToken: CancellationToken.None);
            Assert.That(patched.IsError, Is.Not.True);

            CallToolResult stale = await _client.CallToolAsync(
                "survey_instrument_patch_by_id",
                new Dictionary<string, object?>
                {
                    ["id"] = id.ToString(),
                    ["expectedModifiedUtc"] = firstToken,
                    ["patch"] = new JsonObject { ["Description"] = "stale overwrite" }
                }, cancellationToken: CancellationToken.None);
            Assert.That(stale.IsError, Is.True);
            JsonObject problem = JsonNode.Parse(stale.Content.OfType<TextContentBlock>().Single().Text)!.AsObject();
            Assert.That(problem["error"]?.GetValue<string>(), Is.EqualTo("stale_write"));

            JsonObject latest = await GetInstrument(id);
            Assert.That(latest["Description"]?.GetValue<string>(), Is.EqualTo("patched"));
        }
        finally
        {
            JsonObject latest = await GetInstrument(id);
            await _client.CallToolAsync(
                "survey_instrument_delete_by_id",
                new Dictionary<string, object?>
                {
                    ["id"] = id.ToString(),
                    ["expectedModifiedUtc"] = latest["LastModificationDate"]!.GetValue<string>()
                }, cancellationToken: CancellationToken.None);
        }
    }

    [Test]
    public async Task Batch_export_and_restore_round_trip_is_versioned_and_conflict_safe()
    {
        Guid id = Guid.NewGuid();
        string timestamp = DateTimeOffset.UtcNow.ToString("O");
        var instrument = new JsonObject
        {
            ["MetaInfo"] = new JsonObject { ["ID"] = id.ToString() },
            ["Name"] = "MCP backup test",
            ["CreationDate"] = timestamp,
            ["LastModificationDate"] = timestamp,
            ["ModelType"] = "MWD_WolffDeWardt"
        };
        CallToolResult created = await _client.CallToolAsync("survey_instrument_create",
            new Dictionary<string, object?> { ["surveyInstrument"] = instrument }, cancellationToken: CancellationToken.None);
        Assert.That(created.IsError, Is.Not.True);

        try
        {
            CallToolResult exported = await _client.CallToolAsync("survey_instrument_batch_export",
                new Dictionary<string, object?>
                {
                    ["request"] = new JsonObject
                    {
                        ["Scope"] = "Selected",
                        ["SurveyInstrumentIDs"] = new JsonArray(id.ToString())
                    }
                }, cancellationToken: CancellationToken.None);
            Assert.That(exported.IsError, Is.Not.True);
            JsonObject document = ((JsonObject)exported.StructuredContent!)["data"]!.AsObject();
            Assert.That(document["SchemaVersion"]?.GetValue<int>(), Is.EqualTo(1));

            JsonObject stored = await GetInstrument(id);
            CallToolResult deleted = await _client.CallToolAsync("survey_instrument_delete_by_id",
                new Dictionary<string, object?>
                {
                    ["id"] = id.ToString(),
                    ["expectedModifiedUtc"] = stored["LastModificationDate"]!.GetValue<string>()
                }, cancellationToken: CancellationToken.None);
            Assert.That(deleted.IsError, Is.Not.True);

            CallToolResult restored = await _client.CallToolAsync("survey_instrument_batch_restore",
                new Dictionary<string, object?>
                {
                    ["request"] = new JsonObject
                    {
                        ["ConflictPolicy"] = "FailIfExists",
                        ["CatalogPolicy"] = "MapExisting",
                        ["Document"] = document.DeepClone()
                    }
                }, cancellationToken: CancellationToken.None);
            Assert.That(restored.IsError, Is.Not.True);
            Assert.That((await GetInstrument(id))["Name"]?.GetValue<string>(), Is.EqualTo("MCP backup test"));

            CallToolResult validated = await _client.CallToolAsync("survey_instrument_validate_catalog_references",
                new Dictionary<string, object?> { ["id"] = id.ToString() }, cancellationToken: CancellationToken.None);
            Assert.That(validated.IsError, Is.Not.True);
            Assert.That(((JsonObject)validated.StructuredContent!)["data"]!["Status"]?.GetValue<string>(), Is.EqualTo("valid"));

            CallToolResult audited = await _client.CallToolAsync("survey_instrument_audit_catalog_references",
                new Dictionary<string, object?> { ["offset"] = 0, ["limit"] = 100 }, cancellationToken: CancellationToken.None);
            Assert.That(audited.IsError, Is.Not.True);
            JsonObject audit = ((JsonObject)audited.StructuredContent!)["data"]!.AsObject();
            Assert.That(audit["Results"]!.AsArray().Any(value => value?["SurveyInstrumentID"]?.GetValue<string>() == id.ToString()), Is.True);

            CallToolResult conflict = await _client.CallToolAsync("survey_instrument_batch_restore",
                new Dictionary<string, object?>
                {
                    ["request"] = new JsonObject
                    {
                        ["ConflictPolicy"] = "FailIfExists",
                        ["CatalogPolicy"] = "MapExisting",
                        ["Document"] = document.DeepClone()
                    }
                }, cancellationToken: CancellationToken.None);
            Assert.That(conflict.IsError, Is.True);
        }
        finally
        {
            JsonObject latest = await GetInstrument(id);
            await _client.CallToolAsync("survey_instrument_delete_by_id",
                new Dictionary<string, object?>
                {
                    ["id"] = id.ToString(),
                    ["expectedModifiedUtc"] = latest["LastModificationDate"]!.GetValue<string>()
                }, cancellationToken: CancellationToken.None);
        }
    }

    [Test]
    public async Task Error_source_update_warns_about_unchanged_frozen_snapshots()
    {
        Guid sourceId = Guid.NewGuid();
        Guid instrumentId = Guid.NewGuid();
        string timestamp = DateTimeOffset.UtcNow.ToString("O");
        JsonObject source = new()
        {
            ["MetaInfo"] = new JsonObject { ["ID"] = sourceId.ToString() },
            ["ErrorCode"] = "DRFR",
            ["Description"] = "original template"
        };
        CallToolResult sourceCreated = await _client.CallToolAsync("error_source_create",
            new Dictionary<string, object?> { ["errorSource"] = source }, cancellationToken: CancellationToken.None);
        Assert.That(sourceCreated.IsError, Is.Not.True);

        var instrument = new JsonObject
        {
            ["MetaInfo"] = new JsonObject { ["ID"] = instrumentId.ToString() },
            ["Name"] = "snapshot impact test",
            ["CreationDate"] = timestamp,
            ["LastModificationDate"] = timestamp,
            ["ModelType"] = "MWD_ISCWSA",
            ["ErrorSourceList"] = new JsonArray(source.DeepClone())
        };
        CallToolResult instrumentCreated = await _client.CallToolAsync("survey_instrument_create",
            new Dictionary<string, object?> { ["surveyInstrument"] = instrument }, cancellationToken: CancellationToken.None);
        Assert.That(instrumentCreated.IsError, Is.Not.True);

        try
        {
            CallToolResult versionedRead = await _client.CallToolAsync("error_source_get_by_id",
                new Dictionary<string, object?> { ["id"] = sourceId.ToString() }, cancellationToken: CancellationToken.None);
            string firstVersion = ((JsonObject)versionedRead.StructuredContent!)["versionToken"]!.GetValue<string>();
            source["Description"] = "updated template";
            CallToolResult updated = await _client.CallToolAsync("error_source_update_by_id",
                new Dictionary<string, object?>
                {
                    ["id"] = sourceId.ToString(), ["expectedVersionToken"] = firstVersion, ["errorSource"] = source
                },
                cancellationToken: CancellationToken.None);
            Assert.That(updated.IsError, Is.Not.True);
            JsonObject impact = ((JsonObject)updated.StructuredContent!)["data"]!.AsObject();
            Assert.Multiple(() =>
            {
                Assert.That(impact["AffectedSnapshotCount"]?.GetValue<int>(), Is.EqualTo(1));
                Assert.That(impact["AffectedSurveyInstrumentIDs"]!.AsArray().Single()!.GetValue<string>(), Is.EqualTo(instrumentId.ToString()));
                Assert.That(impact["Warning"]?.GetValue<string>(), Does.Contain("not modified"));
            });

            CallToolResult stale = await _client.CallToolAsync("error_source_update_by_id",
                new Dictionary<string, object?>
                {
                    ["id"] = sourceId.ToString(), ["expectedVersionToken"] = firstVersion, ["errorSource"] = source
                }, cancellationToken: CancellationToken.None);
            Assert.That(stale.IsError, Is.True);
            Assert.That(JsonNode.Parse(stale.Content.OfType<TextContentBlock>().Single().Text)!["error"]?.GetValue<string>(),
                Is.EqualTo("stale_write"));

            JsonObject stored = await GetInstrument(instrumentId);
            Assert.That(stored["ErrorSourceList"]![0]!["Description"]?.GetValue<string>(), Is.EqualTo("original template"));

            JsonObject refreshedSnapshot = source.DeepClone().AsObject();
            refreshedSnapshot["Description"] = "explicitly refreshed snapshot";
            CallToolResult replaced = await _client.CallToolAsync("survey_instrument_error_source_mutate",
                new Dictionary<string, object?>
                {
                    ["id"] = instrumentId.ToString(),
                    ["expectedModifiedUtc"] = stored["LastModificationDate"]!.GetValue<string>(),
                    ["operation"] = "replace",
                    ["errorSource"] = refreshedSnapshot
                }, cancellationToken: CancellationToken.None);
            Assert.That(replaced.IsError, Is.Not.True);
            Assert.That((await GetInstrument(instrumentId))["ErrorSourceList"]![0]!["Description"]?.GetValue<string>(),
                Is.EqualTo("explicitly refreshed snapshot"));
        }
        finally
        {
            JsonObject latest = await GetInstrument(instrumentId);
            await _client.CallToolAsync("survey_instrument_delete_by_id",
                new Dictionary<string, object?>
                {
                    ["id"] = instrumentId.ToString(),
                    ["expectedModifiedUtc"] = latest["LastModificationDate"]!.GetValue<string>()
                }, cancellationToken: CancellationToken.None);
            CallToolResult latestSource = await _client.CallToolAsync("error_source_get_by_id",
                new Dictionary<string, object?> { ["id"] = sourceId.ToString() }, cancellationToken: CancellationToken.None);
            string latestVersion = ((JsonObject)latestSource.StructuredContent!)["versionToken"]!.GetValue<string>();
            await _client.CallToolAsync("error_source_delete_by_id",
                new Dictionary<string, object?>
                {
                    ["id"] = sourceId.ToString(), ["expectedVersionToken"] = latestVersion
                }, cancellationToken: CancellationToken.None);
        }
    }

    private async Task<JsonObject> GetInstrument(Guid id)
    {
        CallToolResult result = await _client.CallToolAsync(
            "survey_instrument_get_by_id",
            new Dictionary<string, object?> { ["id"] = id.ToString() },
            cancellationToken: CancellationToken.None);
        Assert.That(result.IsError, Is.Not.True);
        return ((JsonObject)result.StructuredContent!)["data"]!.AsObject();
    }
}
