using OSDC.DotnetLibraries.General.DataManagement;
using OSDC.DotnetLibraries.Drilling.Surveying;

namespace NORCE.Drilling.SurveyInstrument.ModelTest
{
    public class Tests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
        }

        [Test]
        public void Test_Instanciation()
        {
            ErrorSource errorSource = new()
            {
                MetaInfo = new MetaInfo() { HttpHostName = "https://dev.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "ErrorSource/", ID = new Guid("cc8fbca4-d168-49d2-8f7a-96ea408b9c1c") },
                ErrorCode = ErrorCode.XYM1,
                Description = "Error due to the Misalignment: XY Misalignment 1 error source",
                Index = 30,
                IsSystematic = true,
                IsRandom = false,
                IsGlobal = false,
                IsContinuous = false,
                IsStationary = false,
                Magnitude = 0.1 * Math.PI / 180.0,
                KOperatorImposed = false,
                SingularIssues = false,
                MagnitudeQuantity = "PlaneAngleDrilling",
                UseInclinationInterval = false
            };
            Assert.That(errorSource, Is.Not.Null);
            Assert.That(errorSource.Magnitude, Is.Not.Null);
            Assert.That(errorSource.Magnitude, Is.EqualTo(0.1 * Math.PI / 180.0));

            OSDC.DotnetLibraries.Drilling.Surveying.SurveyInstrument surveyInstrument = new()
            {
                MetaInfo = new MetaInfo() { HttpHostName = "https://dev.digiwells.no/", HttpHostBasePath = "SurveyInstrument/api/", HttpEndPoint = "SurveyInstrument/", ID = new Guid("8ee3d202-47d3-40b2-a8e1-29a4605a025f") },
                Name = "Gyro_ISCWSA",
                Description = "Default Gyro_ISCWSA survey instrument",
                CreationDate = DateTimeOffset.UtcNow,
                LastModificationDate = DateTimeOffset.UtcNow,
                ModelType = SurveyInstrumentModelType.Gyro_ISCWSA,
                UseRelDepthError = false,
                UseMisalignment = false,
                UseTrueInclination = false,
                UseReferenceError = false,
                UseDrillStringMag = false,
                UseGyroCompassError = false,
                CantAngle = 0.0 * Math.PI / 180.0,
                GyroSwitching = 1,
                GyroNoiseRed = 1.0,
                GyroMinDist = 9999,
                ErrorSourceList = [errorSource]
            };
            Assert.That(surveyInstrument, Is.Not.Null);
            Assert.That(surveyInstrument.Name, Is.EqualTo("Gyro_ISCWSA"));
            Assert.That(surveyInstrument.ErrorSourceList, Is.Not.Empty);
            Assert.That(surveyInstrument.ErrorSourceList[0].ErrorCode, Is.EqualTo(ErrorCode.XYM1));
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
        }
    }
}