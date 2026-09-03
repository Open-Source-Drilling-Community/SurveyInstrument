using OSDC.DotnetLibraries.General.DataManagement;
using System;

namespace OSDC.Drilling.SurveyInstrument.Model;

public class SurveyInstrumentIdentityAssignment : IIdentityAssignment
{
    public Guid ID { get; set; }
    public Guid? IdentityID { get; set; }
    public string? Value { get; set; }
}
