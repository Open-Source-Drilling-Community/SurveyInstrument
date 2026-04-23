using NORCE.Drilling.SurveyInstrument.WebPages;

namespace NORCE.Drilling.SurveyInstrument.WebApp;

public class WebPagesHostConfiguration : ISurveyInstrumentWebPagesConfiguration
{
    public string? SurveyInstrumentHostURL { get; set; }
    public string? UnitConversionHostURL { get; set; }
}
