using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using NORCE.Drilling.SurveyInstrument.Service.Controllers;
using NORCE.Drilling.SurveyInstrument.Service.Mcp;
using NORCE.Drilling.SurveyInstrument.Service.Mcp.Tools;
using NUnit.Framework;

namespace ServiceTest;

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
            ["GetAllErrorSourceId"] = "error_source_get_all_ids",
            ["GetAllErrorSourceMetaInfo"] = "error_source_get_all_meta_info",
            ["GetErrorSourceById"] = "error_source_get_by_id",
            ["GetAllErrorSource"] = "error_source_get_all",
            ["PostErrorSource"] = "error_source_create",
            ["PutErrorSourceById"] = "error_source_update_by_id",
            ["DeleteErrorSourceById"] = "error_source_delete_by_id"
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

        Assert.That(endpointMethods, Is.EquivalentTo(EndpointToolMap.Keys));
        Assert.That(_tools.Keys, Is.EquivalentTo(EndpointToolMap.Values.Append("ping")));
    }

    [Test]
    public void Registered_tools_have_unique_names_and_descriptions()
    {
        Assert.That(_tools.Count, Is.EqualTo(EndpointToolMap.Count + 1));
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
}
