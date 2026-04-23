using NORCE.Drilling.SurveyInstrument.ModelShared;

namespace NORCE.Drilling.SurveyInstrument.WebPages;

public interface ISurveyInstrumentAPIUtils
{
    string HostNameSurveyInstrument { get; }
    string HostBasePathSurveyInstrument { get; }
    HttpClient HttpClientSurveyInstrument { get; }
    Client ClientSurveyInstrument { get; }

    string HostNameUnitConversion { get; }
    string HostBasePathUnitConversion { get; }
}
