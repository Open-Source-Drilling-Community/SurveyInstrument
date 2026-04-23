using NORCE.Drilling.SurveyInstrument.ModelShared;

namespace NORCE.Drilling.SurveyInstrument.WebPages;

public class SurveyInstrumentAPIUtils : ISurveyInstrumentAPIUtils
{
    public SurveyInstrumentAPIUtils(ISurveyInstrumentWebPagesConfiguration configuration)
    {
        HostNameSurveyInstrument = Require(configuration.SurveyInstrumentHostURL, nameof(configuration.SurveyInstrumentHostURL));
        HttpClientSurveyInstrument = SetHttpClient(HostNameSurveyInstrument, HostBasePathSurveyInstrument);
        ClientSurveyInstrument = new Client(HttpClientSurveyInstrument.BaseAddress!.ToString(), HttpClientSurveyInstrument);

        HostNameUnitConversion = Require(configuration.UnitConversionHostURL, nameof(configuration.UnitConversionHostURL));
    }

    public string HostNameSurveyInstrument { get; }
    public string HostBasePathSurveyInstrument { get; } = "SurveyInstrument/api/";
    public HttpClient HttpClientSurveyInstrument { get; }
    public Client ClientSurveyInstrument { get; }

    public string HostNameUnitConversion { get; }
    public string HostBasePathUnitConversion { get; } = "UnitConversion/api/";

    private static string Require(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be assigned before WebPages is used.");
        }

        return value;
    }

    private static HttpClient SetHttpClient(string host, string microServiceUri)
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri(host + microServiceUri)
        };
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        return httpClient;
    }
}
