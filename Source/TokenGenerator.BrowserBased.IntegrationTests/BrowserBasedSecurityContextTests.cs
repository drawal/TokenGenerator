using System;
using NUnit.Framework;

namespace FCSAmerica.McGruff.TokenGenerator.BrowserBased.IntegrationTests
{
    [TestFixture]
    public class BrowserBasedSecurityContextTests
    {
        [TestCase("FCSA")]
        [TestCase("NWFCS")]
        public void BrowserBasedSecurityContext_WithECSAddress_GetsTokenAndAuditInfo(string partnerName)
        {
            var ecsAddress = "http://devtitan.FCSAmerica.com/EnterpriseConfigurationStore/v1/RESTServices/api/ConfigItems";

            var securityContext = BrowserBasedSecurityContext.GetInstance(ecsAddress, "DocIndexer", partnerName, forceNewInstance: true);

            Assert.IsNotNull(securityContext);

            Assert.IsNotNull(securityContext.ServiceToken);
            Assert.IsNotNull(securityContext.AuditInfo);
            Console.WriteLine("ApplicationName:" + securityContext.ApplicationName);
            Console.WriteLine("PartnerName:" + securityContext.PartnerName);
            Console.WriteLine("ServiceToken:" + securityContext.ServiceToken);
            Console.WriteLine("AuditInfo:" + securityContext.AuditInfo);

        }

        [Test]
        public void BrowserBasedSecurityContext_WithForceInstanceFalse_GetsSameToken()
        {
            var ecsAddress = "http://devtitan.FCSAmerica.com/EnterpriseConfigurationStore/v1/RESTServices/api/ConfigItems";


            var firstSecurityContext = BrowserBasedSecurityContext.GetInstance(ecsAddress, "DocIndexer", "FCSA", forceNewInstance: false);

            Assert.IsNotNull(firstSecurityContext);
            Assert.IsNotNull(firstSecurityContext.ServiceToken);
            var firstRetrievedServiceToken = firstSecurityContext.ServiceToken;

            Console.WriteLine("ApplicationName:" + firstSecurityContext.ApplicationName);
            Console.WriteLine("PartnerName:" + firstSecurityContext.PartnerName);
            Console.WriteLine("ServiceToken:" + firstSecurityContext.ServiceToken);


            var secondSecurityContext = BrowserBasedSecurityContext.GetInstance(ecsAddress, "DocIndexer", "FCSA", forceNewInstance: false);

            Assert.IsNotNull(secondSecurityContext);
            Assert.IsNotNull(secondSecurityContext.ServiceToken);
            var secondRetrievedServiceToken = secondSecurityContext.ServiceToken;

            Assert.AreEqual(firstRetrievedServiceToken, secondRetrievedServiceToken);


            Console.WriteLine("ServiceToken:" + secondSecurityContext.ServiceToken);



        }

        [Test]
        public void BrowserBasedSecurityContext_WithForceInstanceTrue_GetsDifferentToken()
        {
            var ecsAddress = "http://devtitan.FCSAmerica.com/EnterpriseConfigurationStore/v1/RESTServices/api/ConfigItems";

            var firstSecurityContext = BrowserBasedSecurityContext.GetInstance(ecsAddress, "DocIndexer", "FCSA", forceNewInstance: false);

            Assert.IsNotNull(firstSecurityContext);
            Assert.IsNotNull(firstSecurityContext.ServiceToken);
            var firstRetrievedServiceToken = firstSecurityContext.ServiceToken;

            Console.WriteLine("ApplicationName:" + firstSecurityContext.ApplicationName);
            Console.WriteLine("PartnerName:" + firstSecurityContext.PartnerName);
            Console.WriteLine("FirstServiceToken:" + firstSecurityContext.ServiceToken);


            var secondSecurityContext = BrowserBasedSecurityContext.GetInstance(ecsAddress, "DocIndexer", "FCSA", forceNewInstance: true);

            Assert.IsNotNull(secondSecurityContext);
            Assert.IsNotNull(secondSecurityContext.ServiceToken);
            var secondRetrievedServiceToken = secondSecurityContext.ServiceToken;

            Assert.AreNotEqual(firstRetrievedServiceToken, secondRetrievedServiceToken);
            Console.WriteLine("ApplicationName:" + secondSecurityContext.ApplicationName);
            Console.WriteLine("PartnerName:" + secondSecurityContext.PartnerName);
            Console.WriteLine("SecondServiceToken:" + secondSecurityContext.ServiceToken);
        }

        [TestCase("FCSA")]
        [TestCase("NWFCS")]
        public void BrowserBasedSecurityContext_WithDocuClickProxyAddress_GetTokenAndAuditInfo(string partnerName)
        {
            var ecsAddress = "https://devinternal.fcsamerica.net/DocuClick/v3/REST/api/Proxy/EnterpriseConfigurationStore/v1/ConfigItems/";
            var securityContext = BrowserBasedSecurityContext.GetInstance(ecsAddress, "DocIndexer", partnerName, forceNewInstance: true);

            Assert.IsNotNull(securityContext);


            Assert.IsNotNull(securityContext.ServiceToken);

            Console.WriteLine("ApplicationName:" + securityContext.ApplicationName);
            Console.WriteLine("PartnerName:" + securityContext.PartnerName);
            Console.WriteLine("ServiceToken:" + securityContext.ServiceToken);
            Console.WriteLine("AuditInfo:" + securityContext.AuditInfo);
        }
    }
}

