using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TokenGenerator;

namespace FCSAmerica.McGruff.TokenGenerator.UnitTests
{
    [TestClass]
    public class UnitTest1
    {
        private const string AUDITINFO_SERVICE_ENDPOINT =
            "https://devinternal.fcsamerica.net/mcgruff/v2/rest/api/auditInfo";

        private const string AUTHENTICATION_ENDPOINT = "https://devinternal.fcsamerica.net/mcgruff/adminui/";

        private Dictionary<string, string> _passwords;
        private Dictionary<string, string> Passwords
        {
            get
            {
                if (_passwords == null)
                {
                    _passwords = new Dictionary<string, string>();
                    string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    path = Path.GetDirectoryName(path + "\\..\\..\\..");
                    // passwords.txt is a tab delimited text file with the username in the first field and password in the second.  - this file is not commited to source control.
                    string[] lines = File.ReadAllLines(path + "\\passwords.txt");
                    foreach (var line in lines)
                    {
                        string[] fields = line.Split('\t');
                        _passwords.Add(fields[0], fields[1]);
                    }
                }
                return _passwords;
            }
        }


        [TestMethod]
        public void SecurityContext_GetInstanceWithForceNewInstanceTrue_ReturnsNewInstance()
        {

            var securityContext1 = SecurityContext.GetInstance("MyApplicationName", "MyPartnerName", true);
            var securityContext2 = SecurityContext.GetInstance("MyApplicationName", "MyPartnerName", true);
            Assert.AreNotEqual(securityContext1, securityContext2);
        }

        [TestMethod]
        public void SecurityContext_GetInstance_ReturnsSameInstance()
        {
            var securityContext1 = SecurityContext.GetInstance("MyApplicationName", "MyPartnerName");
            var securityContext2 = SecurityContext.GetInstance("MyApplicationName", "MyPartnerName");
            Assert.AreEqual(securityContext1, securityContext2);
        }




        [TestMethod]
        public void SecurityContextWithDefaultCredendialsAndNoECS_ReturnsValidServiceTokenAndAuditInfo()
        {
            var securityContext = SecurityContext.GetInstance("MyApplicationName", "MyPartnerName", true);
            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;

            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));
            Assert.IsFalse(String.IsNullOrEmpty(auditInfo));
        }


        [TestMethod]
        public void WhenCallingAnExternalService_WithTheGeneratedServiceTokenAndAuditInfo_Returns200StatusCode()
        {
            var securityContext = SecurityContext.GetInstance("MyApplicationName1", "MyPartnerName", true);
            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;

            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            WebClient webClient = new WebClient();
            webClient.Encoding = System.Text.Encoding.UTF8;
            webClient.Headers.Add("Authorization", serviceToken);
            webClient.Headers.Add("FCSA-Audit", auditInfo);

            var responseString =
                webClient.DownloadString("https://devinternal.fcsamerica.net/McGruff/Reference/rest/api/ping/authorize");
            Assert.AreEqual("\"I am Authorize!\"", responseString);

        }
        
        [Ignore]
        [TestMethod]
        public void WhenCallingAnExternalService_WithTheGeneratedServiceTokenAndAuditInfo_ReturnsJsonResponse()
        {
            var securityContext = SecurityContext.GetInstance("MyApplicationName", "MyPartnerName", true);
            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;

            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            WebClient webClient = new WebClient();
            webClient.Encoding = System.Text.Encoding.UTF8;
            webClient.Headers.Add("Authorization", serviceToken);
            webClient.Headers.Add("FCSA-Audit", auditInfo);

            string webException = string.Empty;
            try
            {
                var responseString =
                    webClient.DownloadString(
                        "https://devtitan.fcsamerica.com/CustomerView/v8/rest/api/customers/12345/FCSA");
            }
            catch (WebException ex)
            {
                webException = ex.Message;
            }
            Assert.AreEqual(string.Empty, webException);

        }


        [TestMethod]
        public void RequestingUnExpiredServiceToken__ReturnsSameServiceToken()
        {
            var securityContext = SecurityContext.GetInstance("MyApplicationName", "MyPartnerName", true);
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;

            var serviceToken1 = securityContext.ServiceToken;
            var serviceToken2 = securityContext.ServiceToken;

            Assert.AreEqual(serviceToken1, serviceToken2);

        }


        [Ignore]
        [TestMethod]
        public void RequestingExpiredServiceToken__ReturnsNewServiceToken()
        {
            var securityContext = SecurityContext.GetInstance("MyApplicationName", "MyPartnerName", true);
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;
            securityContext.RefreshMinutesBeforeExpire = 601; // 10 hours and 1 minute.
            var serviceToken1 = securityContext.ServiceToken;
            var serviceToken2 = securityContext.ServiceToken;

            Assert.AreNotEqual(serviceToken1, serviceToken2);

        }

        [Ignore]
        [TestMethod]
        public void SecurityContextWithCredendialsSupplied_ReturnsValidServiceTokenAndAuditInfo()
        {
            var securityContext =
                new SecurityContext(new NetworkCredential("agrilytic_o", Passwords["agrilytic_o"], "fcsamerica"), "MyAppName",
                    "MyPartnerName");
            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;

            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));
            Assert.IsFalse(String.IsNullOrEmpty(auditInfo));
        }


        // [TestMethod]
        public void RequestingAToken_ForTesterL_ReturnsValidToken()
        {
            var securityContext =
                new SecurityContext(new NetworkCredential("testerl", Passwords["testerl"], "fcsamerica"),
                    "MyAppName", "MyPartnerName");
            securityContext.AuditInfoServiceEndpoint = "https://devfcma.fcsamerica.net/mcgruff/web/";
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;

            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));
            Assert.IsFalse(String.IsNullOrEmpty(auditInfo));
        }

        [TestMethod, Ignore]
        public void RequestingAToken_ForFCMATester_ReturnsValidTokenWithTheCorrectPartnerName()
        {
            var securityContext = new SecurityContext(new NetworkCredential("FCSATest2@maky.midam.farm", Passwords["FCSATest2@maky.midam.farm"]),
                "MyAppName", null);
            securityContext.AuditInfoServiceEndpoint = "https://internal.fcsamerica.net/mcgruff/v2/rest/api/auditInfo";
            ;
            securityContext.AuthenticationEndpoint = "https://fcma.fcsamerica.net/mcgruff/web/";

            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));
            Assert.IsFalse(String.IsNullOrEmpty(auditInfo)); // validate partner name is correct.
        }


        [TestMethod]
        public void SecurityContext_SettingEndpoints_CreatesValidTokens()
        {
            var securityContext = SecurityContext.GetInstance("MyAppName", "MyPartnerName", true);
            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;

            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));
            Assert.IsFalse(String.IsNullOrEmpty(auditInfo));
        }


        [Ignore]
        [TestMethod]
        //This test passes when ran one-off but as a batch it doesnt pass.
        public void
            SecurityContextInstance_WithProvidedConstructorWithNetworkCredentialsCalledMoreThanOnce_ReturnsTokenForThoseCredentials()
        {
           var securityContext =
                new SecurityContext(new NetworkCredential("agrilytic_o", Passwords["agrilytic_o"], "fcsamerica"),
                    "MyApplicationName", "MyPartnerName");
            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;


            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            securityContext =
              new SecurityContext(new NetworkCredential("agrilytic_o", Passwords["agrilytic_o"], "fcsamerica"),
                  "MyApplicationName", "MyPartnerName");
            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;


            serviceToken = securityContext.ServiceToken;
            auditInfo = securityContext.AuditInfo;

            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));
            Assert.IsFalse(String.IsNullOrEmpty(auditInfo));
        }

        // [TestMethod]
        public void
            SecurityContextInstance_WithProvidedConstructorWithNetworkCredentialsAndOverwittenHomeRealm_ReturnsTokenForThoseCredentials
            ()
        {
            var securityContext = new SecurityContext(new NetworkCredential("boyerc", "****"), "MyApplicationName",
                "MyPartnerName");
            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT +
                                                     "?homeRealm=dmzfs.fcsamerica.com&realm=devinternal.fcsamerica.net:AGRIPOINT";

            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));
            Assert.IsFalse(String.IsNullOrEmpty(auditInfo));
        }

        // [TestMethod]
        public void SecurityContextInstance_WithOverwittenRealm_ReturnsTokenWithRealm()
        {
            var securityContext = SecurityContext.GetInstance("UnitTestApp", "PartnerA", true);
            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT +
                                                     "?realm=devinternal.fcsamerica.net:AGRIPOINT";


            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));
            Assert.IsFalse(String.IsNullOrEmpty(auditInfo));
        }

        [TestMethod]
        public void SecurityContextInstance_WithOverwittenRelyingParty_ReturnsAuditInfoWithCorrectAppplicationName()
        {
            var securityContext = SecurityContext.GetInstance("UnitTestApp", "PartnerA", true);
            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;
            securityContext.RelyingParty = "devinternal.fcsamerica.net:TokenGenerator";


            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));
            Assert.IsFalse(String.IsNullOrEmpty(auditInfo));
        }


        [TestMethod]
        public void
            SecurityContextInstance_WithOverwittenPartnerNameAndApplicationName_ReturnsAuditInfoWithCorrectPartnerNameAndAppplicationName
            ()
        {
            var securityContext =
                SecurityContext.GetInstance(
                    "http://DevTitan.FCSAmerica.com/EnterpriseConfigurationStore/v1/RESTServices/api/ConfigItems",
                    "MyBogusApplication", "MyBogusPartner", true);
            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            var auditInfoBytes = Convert.FromBase64String(auditInfo);
            var jsonAuditInfo = Encoding.UTF8.GetString(auditInfoBytes);
            JObject jobject = JsonConvert.DeserializeObject<JObject>(jsonAuditInfo);

            Assert.AreEqual("MyBogusPartner", jobject["SignedData"]["PartnerName"]);
            Assert.AreEqual("MyBogusApplication", jobject["SignedData"]["Application"]);
        }

        [TestMethod]
        public void
            SecurityContextInstance_WithOverwittenApplicationName_ReturnsAuditInfoWithPartnerNameFromTokenAndAppplicationName
            ()
        {
            var securityContext =
                SecurityContext.GetInstance(
                    "http://DevTitan.FCSAmerica.com/EnterpriseConfigurationStore/v1/RESTServices/api/ConfigItems",
                    "MyBogusApplication", string.Empty, true);

            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            var auditInfoBytes = Convert.FromBase64String(auditInfo);
            var jsonAuditInfo = Encoding.UTF8.GetString(auditInfoBytes);
            JObject jobject = JsonConvert.DeserializeObject<JObject>(jsonAuditInfo);

            Assert.AreEqual("FCSA", jobject["SignedData"]["PartnerName"]);
            Assert.AreEqual("MyBogusApplication", jobject["SignedData"]["Application"]);
        }

        [Ignore]
        [TestMethod]
        public void
            SecurityContextWithDefaultCredendialsAndNoECSForFCMAEndpoint_ReturnsValidServiceTokenAndResultsFormWebAPICall
            ()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            var securityContext = SecurityContext.GetInstance("MyApplicationName", string.Empty, true);
            securityContext.AuthenticationEndpoint =
                "https://makydevweb10.maky.midam.farm/enterprisetokenawareapplication/?fcsa=";

            var serviceToken = securityContext.ServiceToken;

            HttpWebRequest webRequest =
                (HttpWebRequest)
                    WebRequest.Create(
                        "https://makydevweb10.maky.midam.farm/accountnumberapi/api/LoanNumberService/GetLoanNumber?loanType=L");
            webRequest.Headers.Add("Authorization", serviceToken);
            HttpWebResponse response = (HttpWebResponse) webRequest.GetResponse();
            string result;
            using (var responseStream = response.GetResponseStream())
            {
                using (var reader = new StreamReader(responseStream))
                {
                    result = reader.ReadToEnd();
                }
            }

            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK);
            Assert.IsFalse(String.IsNullOrEmpty(result));
            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));


        }
        
        [Ignore]
        [TestMethod]
        public void
            SecurityContextInstance_WithProvidedConstructorWithNetworkCredentialsForNWFCS_ReturnsTokenForThoseCredentials
            ()
        {
            var securityContext = new SecurityContext(new NetworkCredential("drawal", Passwords["drawal"], "NFCS"),
                "MyApplicationName", "MyPartnerName");
            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = "https://inttestnwfcs.fcsamerica.net/mcgruff/adminui/";


            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));
            Assert.IsFalse(String.IsNullOrEmpty(auditInfo));
        }
        
        [Ignore]
        [TestMethod]
        public void SecurityContextInstance_WithProvidedIdPTokenOverridded_ReturnsValidSTSToken()
        {
            var securityContext =
                SecurityContext.GetInstance(
                    "http://DevTitan.FCSAmerica.com/EnterpriseConfigurationStore/v1/RESTServices/api/ConfigItems",
                    "MyBogusApplication", "MyBogusPartner", true);

            var securityToken =
                new WSTrustTokenGenerator().GetToken(
                    "https://testfs.fcsamerica.com/adfs/services/trust/13/windowsmixed",
                    "https://devsts.fcsamerica.net/");
                securityContext.IdpTokenOverride = securityToken;

            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            var auditInfoBytes = Convert.FromBase64String(auditInfo);
            var jsonAuditInfo = Encoding.UTF8.GetString(auditInfoBytes);
            JObject jobject = JsonConvert.DeserializeObject<JObject>(jsonAuditInfo);

            Assert.AreEqual("MyBogusPartner", jobject["SignedData"]["PartnerName"]);
            Assert.AreEqual("MyBogusApplication", jobject["SignedData"]["Application"]);
        }
        
        [Ignore]
        [TestMethod]
        public void SecurityContextWithDefaultCredendialsAndNoECS_CreatedTwice_ReturnsValidServiceTokenAndAuditInfo()
        {
            var securityContext = SecurityContext.GetInstance("MyApplicationName", "MyPartnerName", true);
            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;

            var serviceToken = securityContext.ServiceToken;
            var auditInfo = securityContext.AuditInfo;

            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));
            Assert.IsFalse(String.IsNullOrEmpty(auditInfo));

            securityContext = new SecurityContext(new NetworkCredential("testerl", Passwords["testerl"],"fcsameria.com"), "MyApplicationName", "MyPartnerName");


            securityContext.AuditInfoServiceEndpoint = AUDITINFO_SERVICE_ENDPOINT;
            securityContext.AuthenticationEndpoint = AUTHENTICATION_ENDPOINT;

            serviceToken = securityContext.ServiceToken;
            auditInfo = securityContext.AuditInfo;

            Assert.IsFalse(String.IsNullOrEmpty(serviceToken));
            Assert.IsFalse(String.IsNullOrEmpty(auditInfo));
        }


    }
}
