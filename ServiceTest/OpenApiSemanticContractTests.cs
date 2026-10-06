using System.Text.Json.Nodes;
using OSDC.DotnetLibraries.Drilling.SemanticCatalogue;

namespace OSDC.Drilling.SurveyInstrument.ServiceTest;

[TestFixture]
public sealed class OpenApiSemanticContractTests
{
    [Test]
    public void Generated_rest_contract_publishes_iscwsa_semantics_and_constraints()
    {
        string root = FindSolutionRoot();
        JsonNode document = JsonNode.Parse(File.ReadAllText(Path.Combine(
            root, "Service", "wwwroot", "json-schema", "SurveyInstrumentMergedModel.json")))!;
        JsonNode schemas = document["components"]!["schemas"]!;
        string errorCode = schemas["ErrorCode"]!["description"]!.GetValue<string>();
        string propagation = schemas["ErrorPropagationMode"]!["description"]!.GetValue<string>();
        JsonNode errorSource = schemas["ErrorSource"]!;
        JsonNode instrument = schemas["SurveyInstrument"]!;

        Assert.Multiple(() =>
        {
            Assert.That(document["paths"]!["/SurveyInstrument"]!["get"]!["summary"]!.GetValue<string>(),
                Is.Not.Empty);
            Assert.That(errorCode, Does.Contain("MFIR and MFI_U/OS/OH/OI"));
            Assert.That(errorCode, Does.Contain("MDIR and MDI_U/OS/OH/OI"));
            Assert.That(propagation, Does.Contain("same survey leg but independent between legs"));
            Assert.That(propagation, Does.Contain("fully correlated across all survey stations, legs, and wells"));
            Assert.That(errorSource["properties"]!["Index"]!["description"]!.GetValue<string>(),
                Does.Contain("Implementation ordering field only"));
            Assert.That(errorSource["properties"]!["KOperatorImposed"]!["description"]!.GetValue<string>(),
                Does.Contain("Opaque legacy compatibility flag"));
            Assert.That(errorSource["allOf"]!.ToJsonString(), Does.Contain("IsContinuous"));
            Assert.That(errorSource["allOf"]!.ToJsonString(), Does.Contain("IsStationary"));
            Assert.That(instrument["properties"]!["CantAngle"]!["description"]!.GetValue<string>(),
                Does.Contain("orthogonal body reference frame"));
            Assert.That(instrument["properties"]!["GyroRunningSpeed"]!["x-si-unit"]!.GetValue<string>(),
                Is.EqualTo("rad/s"));
            Assert.That(errorSource["properties"]!["Magnitude"]!["x-osdc-semantic"]!["concept"]!.GetValue<string>(),
                Is.EqualTo(Concepts.SurveyErrorMagnitude));
            Assert.That(instrument["properties"]!["CantAngle"]!["x-osdc-semantic"]!["reference"]!.GetValue<string>(),
                Is.EqualTo(Concepts.OrthogonalBodyFrameCantConvention));
            Assert.That(instrument["x-osdc-semantic"]!["catalogueVersion"]!.GetValue<string>(), Is.EqualTo("0.15.0"));
        });
    }

    private static string FindSolutionRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "SurveyInstrument.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate SurveyInstrument.sln.");
    }
}
