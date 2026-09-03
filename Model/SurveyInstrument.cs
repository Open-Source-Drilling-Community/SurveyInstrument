using System.Collections.Generic;

namespace OSDC.Drilling.SurveyInstrument.Model;

/// <summary>
/// Survey instrument data enriched with locally managed identity and feature assignments.
/// </summary>
public class SurveyInstrument : OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument
{
    /// <summary>Identity values assigned to this survey instrument.</summary>
    public List<SurveyInstrumentIdentityAssignment>? SurveyInstrumentIdentityAssignments { get; set; }

    /// <summary>Feature options assigned to this survey instrument.</summary>
    public List<SurveyInstrumentFeatureAssignment>? SurveyInstrumentFeatureAssignments { get; set; }
}
