using System.Net.Http.Headers;
using System.Reflection;
using NORCE.Drilling.SurveyInstrument.ModelShared;

namespace ServiceTest
{
    public class Tests
    {
        // testing outside Visual Studio requires using http port (https faces authentication issues both in console and on github)
        private static string host = "http://localhost:8080/";
        //private static string host = "https://localhost:5001/";
        //private static string host = "https://localhost:44368/";
        //private static string host = "http://localhost:54949/";
        private static HttpClient httpClient;
        private static Client nSwagClient;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }; // temporary workaround for testing purposes: bypass certificate validation (not recommended for production environments due to security risks)
            httpClient = new HttpClient(handler);
            httpClient.BaseAddress = new Uri(host + "SurveyInstrument/api/");
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            nSwagClient = new Client(httpClient.BaseAddress.ToString(), httpClient);
        }
        
        public ErrorSource ConstructErrorSource(MetaInfo metaInfo)
        {
            return new ErrorSource
            {
                MetaInfo = metaInfo,
                ErrorCode = ErrorCode.XYM1,
                Description = "default descr",
                Index = 30,
                IsSystematic = true,
                IsRandom = false,
                IsGlobal = false,
                IsContinuous = false,
                IsStationary = false,
                KOperatorImposed = false,
                SingularIssues = false,
                MagnitudeQuantity = "PlaneAngleDrilling",
                UseInclinationInterval = false,
            };
        }

        public SurveyInstrument ConstructSurveyInstrument(MetaInfo metaInfo, List<ErrorSource> errorSourceList)
        {
            DateTimeOffset creationDate = DateTimeOffset.UtcNow;
            return new SurveyInstrument()
            {
                MetaInfo = metaInfo,
                Name = "default name",
                Description = "default descr",
                CreationDate = creationDate,
                LastModificationDate = creationDate,
                ModelType = SurveyInstrumentModelType.Gyro_ISCWSA,
                UseRelDepthError = false,
                UseMisalignment = false,
                UseTrueInclination = false,
                UseReferenceError = false,
                UseDrillStringMag = false,
                UseGyroCompassError = false,
                GyroSwitching = 1,
                GyroNoiseRed = 1.0,
                GyroMinDist = 9999,
                ErrorSourceList = errorSourceList
            };
        }

        [Test]
        public async Task Test_ErrorSource_GET()
        {
            #region post a ErrorSource
            Guid guid = Guid.NewGuid();
            MetaInfo metaInfo = new() { ID = guid };
            ErrorSource errorSource = ConstructErrorSource(metaInfo);

            try
            {
                await nSwagClient.PostErrorSourceAsync(errorSource);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to POST given ErrorSource\n" + ex.Message);
            }
            #endregion

            #region GetAllErrorSourceId
            List<Guid> idList = [];
            try
            {
                idList = (List<Guid>)await nSwagClient.GetAllErrorSourceIdAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET all ErrorSource ids\n" + ex.Message);
            }
            Assert.That(idList, Is.Not.Null);
            Assert.That(idList, Does.Contain(guid));
            #endregion

            #region GetAllErrorSourceMetaInfo
            List<MetaInfo> metaInfoList = [];
            try
            {
                metaInfoList = (List<MetaInfo>)await nSwagClient.GetAllErrorSourceMetaInfoAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET all ErrorSource metainfos\n" + ex.Message);
            }
            Assert.That(metaInfoList, Is.Not.Null);
            IEnumerable<MetaInfo> metaInfoList2 =
                from elt in metaInfoList
                where elt.ID == guid
                select elt;
            Assert.That(metaInfoList2, Is.Not.Null);
            Assert.That(metaInfoList2, Is.Not.Empty);
            #endregion

            #region GetAllErrorSourceById
            ErrorSource? errorSource2 = null;
            try
            {
                errorSource2 = await nSwagClient.GetErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET ErrorSource of given Id\n" + ex.Message);
            }
            Assert.That(errorSource2, Is.Not.Null);
            Assert.That(errorSource2.Description, Is.EqualTo(errorSource.Description));
            #endregion

            #region GetAllErrorSource
            List<ErrorSource> errorSourceList = new();
            try
            {
                errorSourceList = (List<ErrorSource>)await nSwagClient.GetAllErrorSourceAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET the list of ErrorSource\n" + ex.Message);
            }
            Assert.That(errorSourceList, Is.Not.Null);
            IEnumerable<ErrorSource> errorSourceList2 =
                from elt in errorSourceList
                where elt.Description == errorSource.Description
                select elt;
            Assert.That(errorSourceList2, Is.Not.Null);
            Assert.That(errorSourceList2, Is.Not.Empty);
            #endregion

            #region finally delete the new ID
            errorSource2 = null;
            try
            {
                await nSwagClient.DeleteErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to DELETE ErrorSource of given Id\n" + ex.Message);
            }
            try
            {
                errorSource2 = await nSwagClient.GetErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                Assert.That(ex.StatusCode, Is.EqualTo(404));
                TestContext.WriteLine("Impossible to GET ErrorSource of given Id\n" + ex.Message);
            }
            Assert.That(errorSource2, Is.Null);
            #endregion
        }

        [Test]
        public async Task Test_ErrorSource_POST()
        {
            #region retrieve list of ErrorSource
            List<ErrorSource> errorSourceList = new();
            try
            {
                errorSourceList = (List<ErrorSource>)await nSwagClient.GetAllErrorSourceAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to retrieve the list of ErrorSource\n" + ex.Message);
            }
            #endregion

            #region trying to post an empty guid
            Guid guid = Guid.Empty;
            MetaInfo metaInfo = new() { ID = guid };
            ErrorSource errorSource = ConstructErrorSource(metaInfo);

            ErrorSource? errorSource2 = null;
            try
            {
                await nSwagClient.PostErrorSourceAsync(errorSource);
            }
            catch (ApiException ex)
            {
                Assert.That(ex.StatusCode, Is.EqualTo(400));
                TestContext.WriteLine("Impossible to POST ErrorSource with empty Guid\n" + ex.Message);
            }
            try
            {
                errorSource2 = await nSwagClient.GetErrorSourceByIdAsync(Guid.Empty);
            }
            catch (ApiException ex)
            {
                Assert.That(ex.StatusCode, Is.EqualTo(400));
                TestContext.WriteLine("Impossible to GET ErrorSource identified by an empty Guid\n" + ex.Message);
            }
            Assert.That(errorSource2, Is.Null);
            #endregion

            #region post some corrupted data
            // post data with missing input that fails the calculation process
            #endregion

            #region posting a new ID in a valid state
            guid = Guid.NewGuid();
            metaInfo = new() { ID = guid };
            errorSource.MetaInfo = metaInfo;
            try
            {
                await nSwagClient.PostErrorSourceAsync(errorSource);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to POST ErrorSource although it is in a valid state\n" + ex.Message);
            }
            try
            {
                errorSource2 = await nSwagClient.GetErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET the ErrorSource of given Id\n" + ex.Message);
            }
            Assert.That(errorSource2, Is.Not.Null);
            Assert.That(errorSource2.MetaInfo, Is.Not.Null);
            Assert.That(errorSource2.MetaInfo.ID, Is.EqualTo(guid));
            Assert.That(errorSource2.Description, Is.EqualTo(errorSource.Description));
            #endregion

            #region trying to repost the same ID
            bool conflict = false;
            try
            {
                await nSwagClient.PostErrorSourceAsync(errorSource);
            }
            catch (ApiException ex)
            {
                conflict = true;
                Assert.That(ex.StatusCode, Is.EqualTo(409));
                TestContext.WriteLine("Impossible to POST existing ErrorSource\n" + ex.Message);
            }
            Assert.That(conflict, Is.True);
            #endregion

            #region finally delete the new ID
            errorSource2 = null;
            try
            {
                await nSwagClient.DeleteErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to DELETE ErrorSource of given Id\n" + ex.Message);
            }
            try
            {
                errorSource2 = await nSwagClient.GetErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                Assert.That(ex.StatusCode, Is.EqualTo(404));
                TestContext.WriteLine("Impossible to GET deleted ErrorSource of given Id\n" + ex.Message);
            }
            Assert.That(errorSource2, Is.Null);
            #endregion
        }

        [Test]
        public async Task Test_ErrorSource_PUT()
        {
            #region retrieve list of ErrorSource
            List<ErrorSource> errorSourceList = new();
            try
            {
                errorSourceList = (List<ErrorSource>)await nSwagClient.GetAllErrorSourceAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to retrieve the list of ErrorSource\n" + ex.Message);
            }
            #endregion

            #region posting a new ID
            Guid guid = Guid.NewGuid();
            MetaInfo metaInfo = new() { ID = guid };
            ErrorSource errorSource = ConstructErrorSource(metaInfo);

            ErrorSource? errorSource2 = null;
            try
            {
                await nSwagClient.PostErrorSourceAsync(errorSource);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to POST ErrorSource\n" + ex.Message);
            }
            try
            {
                errorSource2 = await nSwagClient.GetErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET the ErrorSource of given Id\n" + ex.Message);
            }
            Assert.That(errorSource2, Is.Not.Null);
            Assert.That(errorSource2.MetaInfo, Is.Not.Null);
            Assert.That(errorSource2.MetaInfo.ID, Is.EqualTo(guid));
            Assert.That(errorSource2.Description, Is.EqualTo(errorSource.Description));
            #endregion

            #region updating the new Id
            errorSource.Description = "modified description";
            try
            {
                await nSwagClient.PutErrorSourceByIdAsync(errorSource.MetaInfo.ID, errorSource);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to PUT ErrorSource of given Id\n" + ex.Message);
            }
            try
            {
                errorSource2 = await nSwagClient.GetErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET the updated ErrorSource of given Id\n" + ex.Message);
            }
            Assert.That(errorSource2, Is.Not.Null);
            Assert.That(errorSource2.MetaInfo, Is.Not.Null);
            Assert.That(errorSource2.MetaInfo.ID, Is.EqualTo(errorSource.MetaInfo.ID));
            Assert.That(errorSource2.Description, Is.EqualTo(errorSource.Description));
            #endregion

            #region finally delete the new ID
            errorSource2 = null;
            try
            {
                await nSwagClient.DeleteErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to DELETE ErrorSource of given Id\n" + ex.Message);
            }
            try
            {
                errorSource2 = await nSwagClient.GetErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                Assert.That(ex.StatusCode, Is.EqualTo(404));
                TestContext.WriteLine("Impossible to GET deleted ErrorSource of given Id\n" + ex.Message);
            }
            Assert.That(errorSource2, Is.Null);
            #endregion
        }

        [Test]
        public async Task Test_ErrorSource_DELETE()
        {
            #region retrieve list of ErrorSource
            List<ErrorSource> errorSourceList = new();
            try
            {
                errorSourceList = (List<ErrorSource>)await nSwagClient.GetAllErrorSourceAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to retrieve the list of ErrorSource\n" + ex.Message);
            }
            #endregion

            #region posting a new ID
            Guid guid = Guid.NewGuid();
            MetaInfo metaInfo = new() { ID = guid };
            ErrorSource errorSource = ConstructErrorSource(metaInfo);

            ErrorSource? errorSource2 = null;
            try
            {
                await nSwagClient.PostErrorSourceAsync(errorSource);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to POST ErrorSource\n" + ex.Message);
            }
            try
            {
                errorSource2 = await nSwagClient.GetErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET the ErrorSource of given Id\n" + ex.Message);
            }
            Assert.That(errorSource2, Is.Not.Null);
            Assert.That(errorSource2.MetaInfo, Is.Not.Null);
            Assert.That(errorSource2.MetaInfo.ID, Is.EqualTo(errorSource.MetaInfo.ID));
            Assert.That(errorSource2.Description, Is.EqualTo(errorSource.Description));
            #endregion

            #region finally delete the new ID
            errorSource2 = null;
            try
            {
                await nSwagClient.DeleteErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to DELETE ErrorSource of given Id\n" + ex.Message);
            }
            try
            {
                errorSource2 = await nSwagClient.GetErrorSourceByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                Assert.That(ex.StatusCode, Is.EqualTo(404));
                TestContext.WriteLine("Impossible to GET deleted ErrorSource of given Id\n" + ex.Message);
            }
            Assert.That(errorSource2, Is.Null);
            #endregion
        }

        [Test]
        public async Task Test_SurveyInstrument_GET()
        {
            #region retrieve list of ErrorSource
            List<ErrorSource> errorSourceList = new();
            try
            {
                errorSourceList = (List<ErrorSource>)await nSwagClient.GetAllErrorSourceAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to retrieve the list of ErrorSource\n" + ex.Message);
            }
            #endregion

            #region post a SurveyInstrument
            Guid guid = Guid.NewGuid();
            MetaInfo metaInfo = new() { ID = guid };
            SurveyInstrument surveyInstrument = ConstructSurveyInstrument(metaInfo, errorSourceList);

            try
            {
                await nSwagClient.PostSurveyInstrumentAsync(surveyInstrument);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to POST given SurveyInstrument\n" + ex.Message);
            }
            #endregion

            #region GetAllSurveyInstrumentId
            List<Guid> idList = [];
            try
            {
                idList = (List<Guid>)await nSwagClient.GetAllSurveyInstrumentIdAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET all SurveyInstrument ids\n" + ex.Message);
            }
            Assert.That(idList, Is.Not.Null);
            Assert.That(idList, Does.Contain(guid));
            #endregion

            #region GetAllSurveyInstrumentMetaInfo
            List<MetaInfo> metaInfoList = [];
            try
            {
                metaInfoList = (List<MetaInfo>)await nSwagClient.GetAllSurveyInstrumentMetaInfoAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET all SurveyInstrument metainfos\n" + ex.Message);
            }
            Assert.That(metaInfoList, Is.Not.Null);
            IEnumerable<MetaInfo> metaInfoList2 =
                from elt in metaInfoList
                where elt.ID == guid
                select elt;
            Assert.That(metaInfoList2, Is.Not.Null);
            Assert.That(metaInfoList2, Is.Not.Empty);
            #endregion

            #region GetAllSurveyInstrumentById
            SurveyInstrument? surveyInstrument2 = null;
            try
            {
                surveyInstrument2 = await nSwagClient.GetSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET SurveyInstrument of given Id\n" + ex.Message);
            }
            Assert.That(surveyInstrument2, Is.Not.Null);
            Assert.That(surveyInstrument2.Name, Is.EqualTo(surveyInstrument.Name));
            #endregion

            #region GetAllSurveyInstrumentLight
            List<SurveyInstrumentLight> surveyInstrumentLightList = [];
            try
            {
                surveyInstrumentLightList = (List<SurveyInstrumentLight>)await nSwagClient.GetAllSurveyInstrumentLightAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET the list of SurveyInstrumentLight\n" + ex.Message);
            }
            Assert.That(surveyInstrumentLightList, Is.Not.Null);
            Assert.That(surveyInstrumentLightList, Is.Not.Empty);
            IEnumerable<SurveyInstrumentLight> surveyInstrumentLightList2 =
                from elt in surveyInstrumentLightList
                where elt.Name == surveyInstrument.Name
                select elt;
            Assert.That(surveyInstrumentLightList2, Is.Not.Null);
            Assert.That(surveyInstrumentLightList2, Is.Not.Empty);
            #endregion

            #region GetAllSurveyInstrument
            List<SurveyInstrument> surveyInstrumentList = new();
            try
            {
                surveyInstrumentList = (List<SurveyInstrument>)await nSwagClient.GetAllSurveyInstrumentAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET the list of SurveyInstrument\n" + ex.Message);
            }
            Assert.That(surveyInstrumentList, Is.Not.Null);
            IEnumerable<SurveyInstrument> surveyInstrumentList2 =
                from elt in surveyInstrumentList
                where elt.Name == surveyInstrument.Name
                select elt;
            Assert.That(surveyInstrumentList2, Is.Not.Null);
            Assert.That(surveyInstrumentList2, Is.Not.Empty);
            #endregion

            #region finally delete the new ID
            surveyInstrument2 = null;
            try
            {
                await nSwagClient.DeleteSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to DELETE SurveyInstrument of given Id\n" + ex.Message);
            }
            try
            {
                surveyInstrument2 = await nSwagClient.GetSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                Assert.That(ex.StatusCode, Is.EqualTo(404));
                TestContext.WriteLine("Impossible to GET SurveyInstrument of given Id\n" + ex.Message);
            }
            Assert.That(surveyInstrument2, Is.Null);
            #endregion
        }

        [Test]
        public async Task Test_SurveyInstrument_POST()
        {
            #region retrieve list of ErrorSource
            List<ErrorSource> errorSourceList = new();
            try
            {
                errorSourceList = (List<ErrorSource>)await nSwagClient.GetAllErrorSourceAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to retrieve the list of ErrorSource\n" + ex.Message);
            }
            #endregion

            #region trying to post an empty guid
            Guid guid = Guid.Empty;
            MetaInfo metaInfo = new() { ID = guid };
            SurveyInstrument surveyInstrument = ConstructSurveyInstrument(metaInfo, errorSourceList);

            SurveyInstrument? surveyInstrument2 = null;
            try
            {
                await nSwagClient.PostSurveyInstrumentAsync(surveyInstrument);
            }
            catch (ApiException ex)
            {
                Assert.That(ex.StatusCode, Is.EqualTo(400));
                TestContext.WriteLine("Impossible to POST SurveyInstrument with empty Guid\n" + ex.Message);
            }
            try
            {
                surveyInstrument2 = await nSwagClient.GetSurveyInstrumentByIdAsync(Guid.Empty);
            }
            catch (ApiException ex)
            {
                Assert.That(ex.StatusCode, Is.EqualTo(400));
                TestContext.WriteLine("Impossible to GET SurveyInstrument identified by an empty Guid\n" + ex.Message);
            }
            Assert.That(surveyInstrument2, Is.Null);
            #endregion

            #region post some corrupted data
            // post data with missing input that fails the calculation process
            #endregion

            #region posting a new ID in a valid state
            guid = Guid.NewGuid();
            metaInfo = new() { ID = guid };
            surveyInstrument.MetaInfo = metaInfo;
            try
            {
                await nSwagClient.PostSurveyInstrumentAsync(surveyInstrument);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to POST SurveyInstrument although it is in a valid state\n" + ex.Message);
            }
            try
            {
                surveyInstrument2 = await nSwagClient.GetSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET the SurveyInstrument of given Id\n" + ex.Message);
            }
            Assert.That(surveyInstrument2, Is.Not.Null);
            Assert.That(surveyInstrument2.MetaInfo, Is.Not.Null);
            Assert.That(surveyInstrument2.MetaInfo.ID, Is.EqualTo(guid));
            Assert.That(surveyInstrument2.Name, Is.EqualTo(surveyInstrument.Name));
            #endregion

            #region trying to repost the same ID
            bool conflict = false;
            try
            {
                await nSwagClient.PostSurveyInstrumentAsync(surveyInstrument);
            }
            catch (ApiException ex)
            {
                conflict = true;
                Assert.That(ex.StatusCode, Is.EqualTo(409));
                TestContext.WriteLine("Impossible to POST existing SurveyInstrument\n" + ex.Message);
            }
            Assert.That(conflict, Is.True);
            #endregion

            #region finally delete the new ID
            surveyInstrument2 = null;
            try
            {
                await nSwagClient.DeleteSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to DELETE SurveyInstrument of given Id\n" + ex.Message);
            }
            try
            {
                surveyInstrument2 = await nSwagClient.GetSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                Assert.That(ex.StatusCode, Is.EqualTo(404));
                TestContext.WriteLine("Impossible to GET deleted SurveyInstrument of given Id\n" + ex.Message);
            }
            Assert.That(surveyInstrument2, Is.Null);
            #endregion
        }

        [Test]
        public async Task Test_SurveyInstrument_PUT()
        {
            #region retrieve list of ErrorSource
            List<ErrorSource> errorSourceList = new();
            try
            {
                errorSourceList = (List<ErrorSource>)await nSwagClient.GetAllErrorSourceAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to retrieve the list of ErrorSource\n" + ex.Message);
            }
            #endregion

            #region posting a new ID
            Guid guid = Guid.NewGuid();
            MetaInfo metaInfo = new() { ID = guid };
            SurveyInstrument surveyInstrument = ConstructSurveyInstrument(metaInfo, errorSourceList);

            SurveyInstrument? surveyInstrument2 = null;
            try
            {
                await nSwagClient.PostSurveyInstrumentAsync(surveyInstrument);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to POST SurveyInstrument\n" + ex.Message);
            }
            try
            {
                surveyInstrument2 = await nSwagClient.GetSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET the SurveyInstrument of given Id\n" + ex.Message);
            }
            Assert.That(surveyInstrument2, Is.Not.Null);
            Assert.That(surveyInstrument2.MetaInfo, Is.Not.Null);
            Assert.That(surveyInstrument2.MetaInfo.ID, Is.EqualTo(guid));
            Assert.That(surveyInstrument2.Name, Is.EqualTo(surveyInstrument.Name));
            #endregion

            #region updating the new Id
            surveyInstrument.Name = "My test SurveyInstrument with modified name";
            surveyInstrument.LastModificationDate = DateTimeOffset.UtcNow;
            try
            {
                await nSwagClient.PutSurveyInstrumentByIdAsync(surveyInstrument.MetaInfo.ID, surveyInstrument);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to PUT SurveyInstrument of given Id\n" + ex.Message);
            }
            try
            {
                surveyInstrument2 = await nSwagClient.GetSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET the updated SurveyInstrument of given Id\n" + ex.Message);
            }
            Assert.That(surveyInstrument2, Is.Not.Null);
            Assert.That(surveyInstrument2.MetaInfo, Is.Not.Null);
            Assert.That(surveyInstrument2.MetaInfo.ID, Is.EqualTo(surveyInstrument.MetaInfo.ID));
            Assert.That(surveyInstrument2.Name, Is.EqualTo(surveyInstrument.Name));
            #endregion

            #region finally delete the new ID
            surveyInstrument2 = null;
            try
            {
                await nSwagClient.DeleteSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to DELETE SurveyInstrument of given Id\n" + ex.Message);
            }
            try
            {
                surveyInstrument2 = await nSwagClient.GetSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                Assert.That(ex.StatusCode, Is.EqualTo(404));
                TestContext.WriteLine("Impossible to GET deleted SurveyInstrument of given Id\n" + ex.Message);
            }
            Assert.That(surveyInstrument2, Is.Null);
            #endregion
        }

        [Test]
        public async Task Test_SurveyInstrument_DELETE()
        {
            #region retrieve list of ErrorSource
            List<ErrorSource> errorSourceList = new();
            try
            {
                errorSourceList = (List<ErrorSource>)await nSwagClient.GetAllErrorSourceAsync();
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to retrieve the list of ErrorSource\n" + ex.Message);
            }
            #endregion

            #region posting a new ID
            Guid guid = Guid.NewGuid();
            MetaInfo metaInfo = new() { ID = guid };
            SurveyInstrument surveyInstrument = ConstructSurveyInstrument(metaInfo, errorSourceList);

            SurveyInstrument? surveyInstrument2 = null;
            try
            {
                await nSwagClient.PostSurveyInstrumentAsync(surveyInstrument);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to POST SurveyInstrument\n" + ex.Message);
            }
            try
            {
                surveyInstrument2 = await nSwagClient.GetSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to GET the SurveyInstrument of given Id\n" + ex.Message);
            }
            Assert.That(surveyInstrument2, Is.Not.Null);
            Assert.That(surveyInstrument2.MetaInfo, Is.Not.Null);
            Assert.That(surveyInstrument2.MetaInfo.ID, Is.EqualTo(surveyInstrument.MetaInfo.ID));
            Assert.That(surveyInstrument2.Name, Is.EqualTo(surveyInstrument.Name));
            #endregion

            #region finally delete the new ID
            surveyInstrument2 = null;
            try
            {
                await nSwagClient.DeleteSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                TestContext.WriteLine("Impossible to DELETE SurveyInstrument of given Id\n" + ex.Message);
            }
            try
            {
                surveyInstrument2 = await nSwagClient.GetSurveyInstrumentByIdAsync(guid);
            }
            catch (ApiException ex)
            {
                Assert.That(ex.StatusCode, Is.EqualTo(404));
                TestContext.WriteLine("Impossible to GET deleted SurveyInstrument of given Id\n" + ex.Message);
            }
            Assert.That(surveyInstrument2, Is.Null);
            #endregion
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            httpClient?.Dispose();
        }
    }
}