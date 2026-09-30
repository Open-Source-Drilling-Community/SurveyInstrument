using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OSDC.DotnetLibraries.Drilling.Surveying;
using OSDC.DotnetLibraries.General.DataManagement;
using OSDC.Drilling.SurveyInstrument.Service;
using OSDC.Drilling.SurveyInstrument.Service.Managers;
using SurveyInstrumentModel = OSDC.Drilling.SurveyInstrument.Model.SurveyInstrument;
using static OSDC.DotnetLibraries.Drilling.Surveying.ErrorSource;

namespace OSDC.Drilling.SurveyInstrument.ServiceTest;

[TestFixture]
public sealed class SurveyInstrumentManagerPersistenceTests
{
    private const double DegreesToRadians = Math.PI / 180.0;

    [Test]
    public void Empty_database_is_seeded_only_with_official_iscwsa_revision5_models_in_si()
    {
        WithManager((manager, unusedConnections) =>
        {
            List<SurveyInstrumentModel> instruments = manager.GetAllSurveyInstrument()!.OfType<SurveyInstrumentModel>().ToList();
            Assert.That(instruments.Select(instrument => instrument.Name), Is.EquivalentTo(new[]
            {
                "ISCWSA MWD (Fixed Rig) Rev5",
                "ISCWSA MWD (Floating Rig) Rev5",
                "ISCWSA MWD+SAG (Fixed Rig) Rev5",
                "ISCWSA MWD+SAG (Floating Rig) Rev5",
                "ISCWSA MWD + Axial Corr. (Fixed Rig) Rev5",
                "ISCWSA MWD + Axial Corr. (Floating Rig) Rev5",
                "ISCWSA MWD + Axial Corr. + SAG (Fixed Rig) Rev5",
                "ISCWSA MWD + Axial Corr. + SAG (Floating Rig) Rev5"
            }));

            foreach (SurveyInstrumentModel instrument in instruments)
            {
                Assert.That(instrument.ErrorSourceList, Is.All.Matches<ErrorSource>(source =>
                    ErrorSourceRevision5.TryValidate(source, requireCurrentCode: true, out _)), instrument.Name);

                ErrorSource decU = instrument.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.DEC_U);
                Assert.That(decU.Magnitude, Is.EqualTo(0.16 * DegreesToRadians).Within(1e-15), instrument.Name);
                Assert.That(decU.EffectivePropagationMode, Is.EqualTo(ErrorPropagationMode.WellByWell), instrument.Name);

                ErrorSource dbhU = instrument.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.DBH_U);
                Assert.That(dbhU.Magnitude, Is.EqualTo(2350.33e-9 * DegreesToRadians).Within(1e-18), instrument.Name);
                Assert.That(dbhU.EffectivePropagationMode, Is.EqualTo(ErrorPropagationMode.WellByWell), instrument.Name);
            }

            foreach (SurveyInstrumentModel nonAxial in instruments.Where(instrument => !instrument.Name!.Contains("Axial Corr.")))
            {
                ErrorSource amil = nonAxial.ErrorSourceList!.Single(source => source.ErrorCode == ErrorCode.AMIL);
                Assert.That(amil.Magnitude, Is.EqualTo(220e-9).Within(1e-18), nonAxial.Name);
                Assert.That(amil.MagnitudeQuantity, Is.EqualTo("EarthMagneticFluxDensity"), nonAxial.Name);
            }

            ErrorCode[] fixedAxialCodes =
            [
                ErrorCode.DRFR, ErrorCode.DSFS, ErrorCode.DSTG,
                ErrorCode.ABIXY_TI1S, ErrorCode.ABIXY_TI2S, ErrorCode.ABIZ,
                ErrorCode.ASIXY_TI1S, ErrorCode.ASIXY_TI2S, ErrorCode.ASIXY_TI3S, ErrorCode.ASIZ,
                ErrorCode.MBIXY_TI1S, ErrorCode.MBIXY_TI2S,
                ErrorCode.MSIXY_TI1S, ErrorCode.MSIXY_TI2S, ErrorCode.MSIXY_TI3S,
                ErrorCode.DECR, ErrorCode.DBHR, ErrorCode.MDIR, ErrorCode.MFIR,
                ErrorCode.XYM1, ErrorCode.XYM2, ErrorCode.XCLA, ErrorCode.XCLH,
                ErrorCode.DEC_U, ErrorCode.DEC_OS, ErrorCode.DBH_U, ErrorCode.DBH_OS,
                ErrorCode.MFI_U, ErrorCode.MFI_OS, ErrorCode.MDI_U, ErrorCode.MDI_OS,
                ErrorCode.SAGE, ErrorCode.XYM3E, ErrorCode.XYM4E,
                ErrorCode.DEC_OH, ErrorCode.DEC_OI, ErrorCode.DBH_OH, ErrorCode.DBH_OI,
                ErrorCode.MFI_OH, ErrorCode.MFI_OI, ErrorCode.MDI_OH, ErrorCode.MDI_OI
            ];
            foreach (SurveyInstrumentModel axial in instruments.Where(instrument => instrument.Name!.Contains("Axial Corr.")))
            {
                ErrorCode[] expectedCodes = axial.Name!.Contains("Floating Rig")
                    ? fixedAxialCodes.Append(ErrorCode.DRFS).ToArray()
                    : fixedAxialCodes;
                Assert.Multiple(() =>
                {
                    Assert.That(axial.ErrorSourceList!.Count, Is.EqualTo(axial.Name!.Contains("Floating Rig") ? 43 : 42), axial.Name);
                    Assert.That(axial.ErrorSourceList.Select(source => source.ErrorCode), Is.EquivalentTo(expectedCodes), axial.Name);
                    Assert.That(axial.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.MFIR).Magnitude,
                        Is.EqualTo(60e-9).Within(1e-18), axial.Name);
                    Assert.That(axial.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.MDIR).Magnitude,
                        Is.EqualTo(0.08 * DegreesToRadians).Within(1e-15), axial.Name);
                    Assert.That(axial.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.MFI_U).Magnitude,
                        Is.EqualTo(61.15e-9).Within(1e-18), axial.Name);
                    Assert.That(axial.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.MFI_OS).Magnitude,
                        Is.EqualTo(88.03e-9).Within(1e-18), axial.Name);
                    Assert.That(axial.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.MFI_OH).Magnitude,
                        Is.EqualTo(72.85e-9).Within(1e-18), axial.Name);
                    Assert.That(axial.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.MFI_OI).Magnitude,
                        Is.EqualTo(13e-9).Within(1e-18), axial.Name);
                    Assert.That(axial.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.MDI_U).Magnitude,
                        Is.EqualTo(0.09 * DegreesToRadians).Within(1e-15), axial.Name);
                    Assert.That(axial.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.MDI_OS).Magnitude,
                        Is.EqualTo(0.14 * DegreesToRadians).Within(1e-15), axial.Name);
                    Assert.That(axial.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.MDI_OH).Magnitude,
                        Is.EqualTo(0.11 * DegreesToRadians).Within(1e-15), axial.Name);
                    Assert.That(axial.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.MDI_OI).Magnitude,
                        Is.EqualTo(0.02 * DegreesToRadians).Within(1e-15), axial.Name);
                });
            }

            foreach (SurveyInstrumentModel floating in instruments.Where(instrument => instrument.Name!.Contains("Floating Rig")))
            {
                Assert.That(floating.ErrorSourceList!.Single(source => source.ErrorCode == ErrorCode.DRFR).Magnitude, Is.EqualTo(2.2));
                Assert.That(floating.ErrorSourceList.Single(source => source.ErrorCode == ErrorCode.DRFS).Magnitude, Is.EqualTo(1.0));
            }

            foreach (SurveyInstrumentModel sagCorrected in instruments.Where(instrument => instrument.Name!.Contains("+SAG")))
            {
                Assert.That(sagCorrected.ErrorSourceList!.Single(source => source.ErrorCode == ErrorCode.SAGE).Magnitude,
                    Is.EqualTo(0.08 * DegreesToRadians).Within(1e-15));
            }
        });
    }

    [Test]
    public void Existing_database_keeps_legacy_records_and_adds_official_revision5_models()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"SurveyInstrumentMigration_{Guid.NewGuid():N}.db");
        using ILoggerFactory loggers = LoggerFactory.Create(builder => builder.ClearProviders());
        try
        {
            var connections = new SqlConnectionManager($"Data Source={path};Pooling=False",
                loggers.CreateLogger<SqlConnectionManager>());
            Guid legacyId = new("3a811f1f-8b54-4952-a6a7-cf584f5e85c8");
            var legacy = new SurveyInstrumentModel
            {
                MetaInfo = new MetaInfo { ID = legacyId },
                Name = "MWD_ISCWSA",
                ModelType = SurveyInstrumentModelType.MWD_WolffDeWardt
            };
            using (SqliteConnection connection = connections.GetConnection()!)
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO SurveyInstrumentTable " +
                    "(ID, MetaInfo, Name, Description, CreationDate, LastModificationDate, SurveyInstrument) " +
                    "VALUES ($id, $meta, $name, '', '', '', $document)";
                command.Parameters.AddWithValue("$id", legacyId.ToString());
                command.Parameters.AddWithValue("$meta", System.Text.Json.JsonSerializer.Serialize(legacy.MetaInfo, JsonSettings.Options));
                command.Parameters.AddWithValue("$name", legacy.Name);
                command.Parameters.AddWithValue("$document", System.Text.Json.JsonSerializer.Serialize(legacy, JsonSettings.Options));
                Assert.That(command.ExecuteNonQuery(), Is.EqualTo(1));
            }

            ConstructorInfo constructor = typeof(SurveyInstrumentManager).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                [typeof(ILogger<SurveyInstrumentManager>), typeof(ILogger<ErrorSourceManager>), typeof(SqlConnectionManager)], null)!;
            var manager = (SurveyInstrumentManager)constructor.Invoke(
                [loggers.CreateLogger<SurveyInstrumentManager>(), loggers.CreateLogger<ErrorSourceManager>(), connections]);

            Assert.Multiple(() =>
            {
                Assert.That(manager.GetSurveyInstrumentById(legacyId)?.Name, Is.EqualTo("MWD_ISCWSA"));
                Assert.That(manager.GetAllSurveyInstrument()!.OfType<SurveyInstrumentModel>()
                    .Count(instrument => instrument.Name!.StartsWith("ISCWSA MWD")), Is.EqualTo(8));
                Assert.That(manager.Count, Is.EqualTo(9));
            });
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public void Create_and_update_accept_apostrophes_and_create_assigns_server_timestamps()
    {
        WithManager((manager, _) =>
        {
            var instrument = new SurveyInstrumentModel
            {
                MetaInfo = new MetaInfo { ID = Guid.NewGuid() },
                Name = "U1's instrument",
                Description = "Main features of well U1's survey instrument",
                CreationDate = DateTimeOffset.UnixEpoch,
                LastModificationDate = DateTimeOffset.UnixEpoch,
                ModelType = SurveyInstrumentModelType.MWD_WolffDeWardt
            };

            DateTimeOffset before = DateTimeOffset.UtcNow;
            Assert.That(manager.AddSurveyInstrument(instrument), Is.True);
            DateTimeOffset after = DateTimeOffset.UtcNow;
            SurveyInstrumentModel stored = manager.GetSurveyInstrumentById(instrument.MetaInfo.ID)!;

            Assert.Multiple(() =>
            {
                Assert.That(stored.Name, Is.EqualTo(instrument.Name));
                Assert.That(stored.Description, Is.EqualTo(instrument.Description));
                Assert.That(stored.CreationDate, Is.InRange(before, after));
                Assert.That(stored.LastModificationDate, Is.EqualTo(stored.CreationDate));
            });

            stored.Description = "It's still valid after an update";
            Assert.That(manager.UpdateSurveyInstrumentById(stored.MetaInfo!.ID, stored, stored.LastModificationDate), Is.True);
            Assert.That(manager.GetSurveyInstrumentById(stored.MetaInfo.ID)!.Description, Is.EqualTo(stored.Description));
        });
    }

    [Test]
    public void Legacy_record_without_timestamps_can_be_updated_with_the_visible_epoch_token()
    {
        WithManager((manager, connections) =>
        {
            var instrument = new SurveyInstrumentModel
            {
                MetaInfo = new MetaInfo { ID = Guid.NewGuid() },
                Name = "Legacy instrument",
                ModelType = SurveyInstrumentModelType.MWD_WolffDeWardt
            };
            using (SqliteConnection connection = connections.GetConnection()!)
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO SurveyInstrumentTable " +
                    "(ID, MetaInfo, Name, Description, CreationDate, LastModificationDate, SurveyInstrument) " +
                    "VALUES ($id, $meta, $name, '', '', '', $document)";
                command.Parameters.AddWithValue("$id", instrument.MetaInfo.ID);
                command.Parameters.AddWithValue("$meta", System.Text.Json.JsonSerializer.Serialize(instrument.MetaInfo, JsonSettings.Options));
                command.Parameters.AddWithValue("$name", instrument.Name);
                command.Parameters.AddWithValue("$document", System.Text.Json.JsonSerializer.Serialize(instrument, JsonSettings.Options));
                Assert.That(command.ExecuteNonQuery(), Is.EqualTo(1));
            }

            SurveyInstrumentModel stored = manager.GetSurveyInstrumentById(instrument.MetaInfo.ID)!;
            Assert.That(stored.LastModificationDate, Is.EqualTo(DateTimeOffset.UnixEpoch));
            stored.Description = "Claude's update";
            Assert.That(manager.UpdateSurveyInstrumentById(stored.MetaInfo!.ID, stored, stored.LastModificationDate), Is.True);
        });
    }

    private static void WithManager(Action<SurveyInstrumentManager, SqlConnectionManager> assertion)
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"SurveyInstrumentPersistence_{Guid.NewGuid():N}.db");
        using ILoggerFactory loggers = LoggerFactory.Create(builder => builder.ClearProviders());
        try
        {
            var connections = new SqlConnectionManager($"Data Source={path};Pooling=False",
                loggers.CreateLogger<SqlConnectionManager>());
            ConstructorInfo constructor = typeof(SurveyInstrumentManager).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                [typeof(ILogger<SurveyInstrumentManager>), typeof(ILogger<ErrorSourceManager>), typeof(SqlConnectionManager)], null)!;
            var manager = (SurveyInstrumentManager)constructor.Invoke(
                [loggers.CreateLogger<SurveyInstrumentManager>(), loggers.CreateLogger<ErrorSourceManager>(), connections]);
            assertion(manager, connections);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
