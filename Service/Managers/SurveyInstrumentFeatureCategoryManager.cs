using OSDC.DotnetLibraries.General.DataManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OSDC.Drilling.SurveyInstrument.Service.Managers;

public sealed class SurveyInstrumentFeatureCategoryManager
{
    private static readonly (string Name, bool Exclusive, bool Validity, string[] Options)[] Defaults =
    [
        ("MeasurementPrinciple", false, false, ["Magnetic", "Gyroscopic", "Inertial", "Accelerometer", "Gravimetric", "InclinationOnly", "AzimuthOnly", "InclinationAndAzimuth", "DepthTracking", "Unknown"]),
        ("AzimuthReference", true, false, ["MagneticNorth", "TrueNorth", "GridNorth", "GyroNorthSeeking", "GyroContinuous", "ExternalReference", "Unknown"]),
        ("SurveyMode", false, false, ["StaticSurvey", "StationarySurvey", "ContinuousSurvey", "RotatingSurvey", "SlidingSurvey", "WhileDrillingSurvey", "MemorySurvey", "RealTimeSurvey", "PostRunSurvey", "MultiShotSurvey", "SingleShotSurvey", "Unknown"]),
        ("RunningMode", false, false, ["MWD", "LWD", "RSSIntegrated", "Wireline", "Slickline", "CoiledTubing", "GyroWhileDrilling", "DropGyro", "PumpDown", "PumpedInPipe", "MemoryTool", "SurfaceReadout", "RigFloorInstrument", "Unknown"]),
        ("MeasurementTimingCondition", false, false, ["AtConnection", "AtPumpStartup", "AtPumpStop", "PumpsOff", "PumpsOn", "OnBottom", "OffBottom", "DuringSliding", "DuringRotating", "DuringTrippingIn", "DuringTrippingOut", "DuringReaming", "DuringBackreaming", "DuringCirculation", "DuringStationaryPeriod", "Continuous", "OnDemand", "Scheduled", "Unknown"]),
        ("CalibrationMode", false, true, ["FactoryCalibration", "LaboratoryCalibration", "SurfaceCalibration", "RigSiteCalibration", "InFieldCalibration", "DownholeCalibration", "PreRunCalibration", "PostRunCalibration", "InRunCalibration", "ContinuousCalibration", "DynamicCalibration", "StaticCalibration", "NorthSeekingCalibration", "MagneticReferenceCalibration", "SagCalibration", "BiasCalibration", "ScaleFactorCalibration", "MisalignmentCalibration", "TemperatureCalibration", "Unknown"]),
        ("CorrectionCapability", false, false, ["MagneticDeclinationCorrection", "GridConvergenceCorrection", "DipAngleCorrection", "TotalMagneticFieldCorrection", "DrillstringMagneticInterferenceCorrection", "AxialMagneticInterferenceCorrection", "CrossAxialMagneticInterferenceCorrection", "SagCorrection", "BHAInterferenceCorrection", "TemperatureCompensation", "VibrationCompensation", "RotationCompensation", "MisalignmentCorrection", "BiasCorrection", "ScaleFactorCorrection", "MultiStationAnalysis", "InFieldReferencing", "Unknown"]),
        ("QualityControlCapability", false, false, ["MagneticFieldStrengthCheck", "DipAngleCheck", "GravityMagnitudeCheck", "ToolfaceStabilityCheck", "InclinationStabilityCheck", "AzimuthStabilityCheck", "ShockAndVibrationCheck", "TemperatureLimitCheck", "RotationStatusCheck", "StationarityCheck", "SurveyRepeatabilityCheck", "MultiStationQC", "EllipseOfUncertaintyOutput", "ErrorModelOutput", "Unknown"]),
        ("ToolfaceCapability", false, false, ["NoToolface", "GravityToolface", "MagneticToolface", "GyroToolface", "ContinuousToolface", "StationaryToolface", "RotatingToolface", "Unknown"]),
        ("OutputDataType", false, false, ["Inclination", "Azimuth", "Toolface", "HighSideToolface", "MagneticToolface", "GravityToolface", "MeasuredDepth", "DoglegSeverity", "NorthSouthPosition", "EastWestPosition", "TVD", "SurveyStation", "ContinuousTrajectory", "RawSensorData", "ErrorModel", "UncertaintyEllipse", "QualityFlags", "Unknown"]),
        ("TelemetryMode", false, false, ["MudPulse", "Electromagnetic", "WiredDrillPipe", "Acoustic", "WirelineTelemetry", "MemoryOnly", "SurfaceCable", "ManualReadout", "RealTimeStreaming", "BatchDownload", "Unknown"]),
        ("PowerMode", false, false, ["BatteryPowered", "TurbinePowered", "WiredPower", "MudFlowPowered", "SurfacePowered", "MemoryBattery", "Unknown"]),
        ("OperatingEnvironment", false, false, ["StandardTemperature", "HighTemperature", "HighPressure", "HPHT", "HighShock", "HighVibration", "HighDogleg", "SlimHole", "LargeHole", "CasedHole", "OpenHole", "NearCasing", "NearMagneticInterference", "NonMagneticBHARequired", "Unknown"]),
        ("SurveyApplication", false, false, ["DirectionalDrilling", "Geosteering", "VerticalityControl", "AntiCollision", "ReliefWellIntercept", "CasingExit", "Sidetrack", "MultilateralJunction", "WellPlacement", "FinalSurvey", "GyroTieIn", "NorthReferenceEstablishment", "Unknown"]),
        ("DataProcessingMode", false, false, ["RawMeasurement", "CorrectedMeasurement", "RealTimeProcessed", "PostProcessed", "MemoryDownloaded", "ManuallyEntered", "AutomaticallyValidated", "ManuallyValidated", "UncertaintyPropagated", "Unknown"]),
        ("CertificationStatus", true, true, ["Certified", "CalibrationValid", "CalibrationExpired", "UnderMaintenance", "OutOfService", "Rejected", "Unknown"])
    ];

    private readonly SurveyInstrumentCatalogStore<Model.SurveyInstrumentFeatureCategory> store;
    private readonly SqlConnectionManager connections;

    public SurveyInstrumentFeatureCategoryManager(SqlConnectionManager connections)
    {
        this.connections = connections;
        store = new(connections, "SurveyInstrumentFeatureCategoryTable", "SurveyInstrumentFeatureCategory",
            value => value.MetaInfo, value => value.Name, (value, date) => value.CreationDate = date,
            (value, date) => value.LastModificationDate = date, value => value.IsExclusive,
            value => value.HasValidityPeriod);
    }

    public List<Model.SurveyInstrumentFeatureCategory> GetAll()
    {
        EnsureDefaults();
        return store.All();
    }

    public Model.SurveyInstrumentFeatureCategory? Get(Guid id) => store.ById(id);

    public bool Add(Model.SurveyInstrumentFeatureCategory value)
    {
        Prepare(value);
        return store.Add(value);
    }

    public bool Update(Guid id, Model.SurveyInstrumentFeatureCategory value)
    {
        Prepare(value);
        return !RemovesReferencedOptions(id, value) && store.Update(id, value);
    }

    public bool Delete(Guid id) => !IsReferenced(id) && store.Delete(id);

    public bool IsReferenced(Guid id) => ReadSurveyInstruments().Any(value =>
        value.SurveyInstrumentFeatureAssignments?.Any(assignment => assignment.FeatureCategoryID == id) == true);

    private bool RemovesReferencedOptions(Guid id, Model.SurveyInstrumentFeatureCategory value)
    {
        HashSet<Guid> retained = (value.Options ?? []).Select(option => option.ID).ToHashSet();
        return ReadSurveyInstruments().Any(instrument => instrument.SurveyInstrumentFeatureAssignments?.Any(assignment =>
            assignment.FeatureCategoryID == id && assignment.FeatureOptionID is Guid option && !retained.Contains(option)) == true);
    }

    private IEnumerable<Model.SurveyInstrument> ReadSurveyInstruments()
    {
        using var connection = connections.GetConnection();
        using var command = connection!.CreateCommand();
        command.CommandText = "SELECT SurveyInstrument FROM SurveyInstrumentTable";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            Model.SurveyInstrument? value = JsonSerializer.Deserialize<Model.SurveyInstrument>(reader.GetString(0), JsonSettings.Options);
            if (value != null)
            {
                yield return value;
            }
        }
    }

    private void EnsureDefaults()
    {
        if (store.All().Count > 0)
        {
            return;
        }
        foreach (var item in Defaults)
        {
            Add(new()
            {
                MetaInfo = new MetaInfo { ID = SurveyInstrumentCatalogId.For($"feature:{item.Name}") },
                Name = item.Name,
                IsExclusive = item.Exclusive,
                HasValidityPeriod = item.Validity,
                Options = item.Options.Select(name => new Model.SurveyInstrumentFeatureOption
                    { ID = SurveyInstrumentCatalogId.For($"feature:{item.Name}:option:{name}"), Name = name }).ToList()
            });
        }
    }

    private static void Prepare(Model.SurveyInstrumentFeatureCategory value)
    {
        value.Options ??= [];
        foreach (Model.SurveyInstrumentFeatureOption option in value.Options)
        {
            if (option.ID == Guid.Empty)
            {
                option.ID = Guid.NewGuid();
            }
        }
    }
}
