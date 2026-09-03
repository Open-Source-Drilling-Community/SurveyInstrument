using OSDC.Drilling.SurveyInstrument.WebPages;

namespace OSDC.Drilling.SurveyInstrument.WebApp;

public class WebPagesHostConfiguration : ISurveyInstrumentWebPagesConfiguration
{
    public string? SurveyInstrumentHostURL { get; set; }
    public string? UnitConversionHostURL { get; set; }
}
