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
