public static class APIUtils
{
    // API parameters
    public static readonly string HostNameSurveyInstrument = NORCE.Drilling.SurveyInstrument.WebApp.Configuration.SurveyInstrumentHostURL!;
    public static readonly string HostBasePathSurveyInstrument = "SurveyInstrument/api/";
    public static readonly HttpClient HttpClientSurveyInstrument = APIUtils.SetHttpClient(HostNameSurveyInstrument, HostBasePathSurveyInstrument);
    public static readonly NORCE.Drilling.SurveyInstrument.ModelShared.Client ClientSurveyInstrument = new NORCE.Drilling.SurveyInstrument.ModelShared.Client(APIUtils.HttpClientSurveyInstrument.BaseAddress!.ToString(), APIUtils.HttpClientSurveyInstrument);

    public static readonly string HostNameUnitConversion = NORCE.Drilling.SurveyInstrument.WebApp.Configuration.UnitConversionHostURL!;
    public static readonly string HostBasePathUnitConversion = "UnitConversion/api/";

    // API utility methods
    public static HttpClient SetHttpClient(string host, string microServiceUri)
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }; // temporary workaround for testing purposes: bypass certificate validation (not recommended for production environments due to security risks)
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri(host + microServiceUri)
        };
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        return httpClient;
    }
}