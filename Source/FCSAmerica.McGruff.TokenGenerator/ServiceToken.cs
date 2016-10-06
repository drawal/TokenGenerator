using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Xml;
using FCSAmerica.McGruff.TokenGenerator.Settings;

namespace FCSAmerica.McGruff.TokenGenerator
{

    public class ServiceToken
    {
        private string _credentialUri;
        private static object _tokenLock = new object();
        private static object _auditInfoLock = new object();

        private const string DefaultServiceProviderPartnerName = "FCSA";

        private string _authenticationEndpoint;

        public string AuthenticationEndpoint
        {
            get
            {
                if (String.IsNullOrEmpty(_authenticationEndpoint))
                {
                    _authenticationEndpoint = this.ConfigItems["v2.AuthenticationEndpoint"];
                }
                return GetUrl(_authenticationEndpoint);
            }
            set { _authenticationEndpoint = value; }
        }

        private string _auditInfoServiceEndpoint;

        public string AuditInfoServiceEndpoint
        {
            get
            {
                if (String.IsNullOrEmpty(_auditInfoServiceEndpoint))
                {
                    string url = this.ConfigItems["v2.ClaimsMapperUri"];
                    url = (url.EndsWith("/") ? url : url + "/") + "auditInfo";
                    if (!String.IsNullOrEmpty(PartnerName) && !string.IsNullOrEmpty(ApplicationName))
                    {
                        url += "/" + this.PartnerName + "/" + this.ApplicationName;
                    }
                    return url;
                }
                return _auditInfoServiceEndpoint;
            }
            set { _auditInfoServiceEndpoint = value; }
        }

        private string _relyingParty;

        public string RelyingParty
        {
            get { return _relyingParty; }
            set { _relyingParty = value; }

        }

        private string _identityProvider;

        public string IdentityProvider
        {
            get { return _identityProvider; }
            set { _identityProvider = value; }
        }


        private string _applicationName;

        public string ApplicationName
        {
            get { return _applicationName; }
            set { _applicationName = value; }
        }

        private string _partnerName;

        public string PartnerName
        {
            get
            {
                if (String.IsNullOrEmpty(_partnerName))
                {
                    try
                    {
                        // get this value from ECS if it exists.
                        var authenticationUri = new Uri(this.AuthenticationEndpoint);
                        var partnerHostMapping = "partnerMapping:" + authenticationUri.Host;
                        _partnerName = this.ConfigItems[partnerHostMapping];
                    }
                    catch
                    {
                        // do nothing.. just gracefully move on/
                    }

                }
                return _partnerName;
            }
            set { _partnerName = value; }
        }
        public DateTime TokenExpireDate
        {
            get
            {
                return _tokenExpireDate;
            }
        }

        private DateTime _tokenExpireDate = DateTime.MaxValue;
        protected string _token;

        public string Token
        {
            get
            {
                if (_token == null || IsExpiredOrAboutToExpire)
                {
                    // keeps threaded
                    lock (_tokenLock)
                    {
                        RefreshToken();
                    }
                }
                return ToBase64(_token);
            }
            //set { _token = value; }
        }

        private string _auditInfo;

        public string AuditInfo
        {
            get
            {
                if (HttpContext.Current != null && HttpContext.Current.Request.Headers["FCSA-Audit"] != null)
                {
                    return HttpContext.Current.Request.Headers["FCSA-Audit"].ToString();
                }

                if (_auditInfo == null)
                {
                    lock (_auditInfoLock)
                    {
                        _auditInfo = RefreshAuditInfo().Replace("\"", "");
                    }
                }
                return _auditInfo;  // _auditInfo is already encoded.
            }
            set
            {
                _auditInfo = value;
            }
        }

        private string _ecsServiceAddress;

        public string ECSServiceAddress
        {
            get { return GetUrl(_ecsServiceAddress) + "mcgruff"; }
        }

        private CookieContainer cookieContainer;

        public NetworkCredential NetworkCredential { get; set; }


        private int _refreshMinutesBeforeExpire = 10;
        public int RefreshMinutesBeforeExpire
        {
            get { return _refreshMinutesBeforeExpire; }
            set { _refreshMinutesBeforeExpire = value; }
        }

        private string _idpTokenOverride;
        public string IdpTokenOverride
        {
            get { return _idpTokenOverride; }
            set { _idpTokenOverride = value; }
        }

        public ServiceToken(string ecsServiceAddress, NetworkCredential credential, string applicationName, string partnerName)
        {
            _ecsServiceAddress = ecsServiceAddress;
            NetworkCredential = credential;
            ApplicationName = applicationName;
            PartnerName = partnerName;
        }

        public ServiceToken(string ecsServiceAddress, string applicationName, string partnerName)
        {

            _ecsServiceAddress = ecsServiceAddress;
            ApplicationName = applicationName;
            PartnerName = partnerName;
        }


        private Dictionary<string, string> _configItems;

        public Dictionary<string, string> ConfigItems
        {
            get
            {
                if (_configItems == null)
                {
                    _configItems = new Dictionary<string, string>();

                    var request = GetWebRequest(ECSServiceAddress);
                    string json = GetResponseContent(request.GetResponse());
                    request.Abort();

                    JavaScriptSerializer serializer = new JavaScriptSerializer();

                    var allConfigList = serializer.Deserialize<EcsConfiguration>(json);

                    //Get settings from all partners
                    var allPartnerConfigurationList = allConfigList.ConfigurationList.Single(_ => _.PartnerId == "");
                    var allPartnerConfigurationSettings = allPartnerConfigurationList.ConfigurationSettings;
                    allPartnerConfigurationSettings.ForEach((x) => { _configItems[x.Key] = x.Value; });

                    //Get settings for specific partners
                    var partnerId = String.IsNullOrEmpty(_partnerName) ? DefaultServiceProviderPartnerName : _partnerName;
                    var partnerSpecificConfigurationList = allConfigList.ConfigurationList.FirstOrDefault(_ => _.PartnerId == partnerId);
                    if (partnerSpecificConfigurationList != null)
                    {
                        var partnerSpecificConfigurationSettings = partnerSpecificConfigurationList.ConfigurationSettings;
                        
                        //replace settings in all settings if exists in partner
                        partnerSpecificConfigurationSettings.ForEach((x) =>
                        {
                            if (_configItems.Keys.Contains(x.Key))
                            {
                                _configItems[x.Key] = x.Value;
                            }
                            else
                            {
                                _configItems.Add(x.Key,x.Value);
                            }
                        });
                    }
                }
                return _configItems;
            }
        }



        private HttpWebRequest _request;
        private CookieContainer _cookieContainer;
        public WebRequest GetWebRequest(string uri)
        {
            int tmp = ServicePointManager.DefaultConnectionLimit;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AllowAutoRedirect = true;


            _cookieContainer = _cookieContainer ?? new CookieContainer();
            request.CookieContainer = _cookieContainer;

            if (NetworkCredential != null)
            {
                var credentialCache = new CredentialCache();
                if (String.IsNullOrEmpty(_credentialUri))
                {
                    // _credentialUri should be something like:  https://fs.fcsamerica.com
                    _credentialUri = GetCredentialHost(uri);
                }

                credentialCache.Add(new Uri(_credentialUri), "Digest", NetworkCredential);
                credentialCache.Add(new Uri(_credentialUri), "Kerberos", NetworkCredential);
                credentialCache.Add(new Uri(_credentialUri), "NTLM", NetworkCredential);
                credentialCache.Add(new Uri(_credentialUri), "Basic", NetworkCredential);
                credentialCache.Add(new Uri(_credentialUri), "Negotiate", NetworkCredential);

                request.Credentials = credentialCache;
                // request.PreAuthenticate = true;

                //string basicCredentialsEncoded = Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(NetworkCredential.UserName + ":" + NetworkCredential.Password));
                //request.Headers.Add("Authorization", "Basic " + basicCredentialsEncoded);
            }
            else
            {
                request.UseDefaultCredentials = true;
            }

            request.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)";
            request.Accept = "*/*";
            return request;
        }


        protected virtual void RefreshToken()
        {
            WebResponse response;
            var request = GetWebRequest(this.AuthenticationEndpoint);

            try
            {
                response = request.GetResponse();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            string body;
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                body = reader.ReadToEnd();
            }

            var stsUrl = FindSTSUrlForm(body);
            var idpToken = GetTokenFromBody(body);

            if (String.IsNullOrEmpty(idpToken) || !String.IsNullOrEmpty(IdpTokenOverride))
            {
                idpToken = IdpTokenOverride;
            }

            response.Close();
            request.Abort();

            _token = GetSTSTokenFromIdpToken(stsUrl, idpToken, true);

            SetExpireDateFromToken();
        }


        private string RefreshAuditInfo()
        {
            var request = GetWebRequest(this.AuditInfoServiceEndpoint);
            request.Headers.Add("Authorization", "SAML " + this.Token);

            string auditInfo = GetResponseContent(request.GetResponse());

            request.Abort();
            return auditInfo;
        }

        private string GetResponseContent(WebResponse response)
        {
            string content = string.Empty;
            using (var responseStream = response.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    content = reader.ReadToEnd();
                }
            }
            response.Close();
            return content;
        }

        public string GetSTSTokenFromIdpToken(string stsUrl, string idpToken, bool cleanToken)
        {
            var request = GetWebRequest(stsUrl);


            var postContent = GetFormPost(idpToken);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            using (var writer = new StreamWriter(request.GetRequestStream()))
            {
                writer.Write(postContent);
            }

            string stsBody = GetResponseContent(request.GetResponse());


            request.Abort();

            var stsToken = GetTokenFromBody(stsBody);

            if (cleanToken)
            {
                // need to clean token.
                return CleanToken(stsToken);
            }
            return stsToken;
        }

        protected string CleanToken(string stsToken)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(stsToken);
            var node = doc.SelectSingleNode("//*[local-name()='Assertion']");
            if (node == null)
            {
                return null;
            }
            return node.OuterXml;
        }

        private string FindSTSUrlForm(string body)
        {
            var formUrlExpression = new Regex("<form.+?action=\"(.+?)\"");
            var groups = formUrlExpression.Match(body).Groups;
            string url = groups.Count > 0 ? groups[1].Value : null;
            /* if (url != null && url.StartsWith("/"))
             {
                 // it is a relative path, and the STS seems to be on the same box as the AuthenticationEndpoint,
                 // we need to fully qualify the path.
                 var authenticationEndpointUri = new Uri(this.AuthenticationEndpoint);
                 return authenticationEndpointUri.Scheme + "://" + authenticationEndpointUri.DnsSafeHost + url;
             }*/
            return url;
        }

        private string GetTokenFromBody(string body)
        {
            var tokenExpression = new Regex("<input.+?name=\"wresult\".+?value=\"(.+?)\"");
            var token = HttpUtility.HtmlDecode(tokenExpression.Match(body).Groups[1].Value);
            return token;
        }

        private string GetFormPost(string token)
        {
            return String.Format("wa=wsignin1.0&wresult={0}&wctx=rm=0&id=passive&ru=%2fmcgruff%2fweb%2f",
                HttpUtility.UrlEncode(token));
        }

        private string ToBase64(string token)
        {
            byte[] bytesToEncode = Encoding.UTF8.GetBytes(token);
            return Convert.ToBase64String(bytesToEncode);
        }


        public bool IsExpiredOrAboutToExpire
        {
            get
            {
                return _tokenExpireDate.Subtract(new TimeSpan(0, 0, _refreshMinutesBeforeExpire, 0)) < DateTime.UtcNow;
            }
        }

        protected void SetExpireDateFromToken()
        {
            XmlDocument samlToken = new XmlDocument();
            samlToken.LoadXml(_token);

            DateTime defaultExpireDate = DateTime.Now.ToUniversalTime().AddYears(-1);
            string innerText;

            var nod = samlToken.SelectSingleNode("//*[local-name()='Expires']") as XmlElement;
            if (nod != null)
            {
                innerText = nod.InnerText;
            }
            else
            {
                var attr = samlToken.SelectSingleNode("//*[local-name()='Conditions']/@NotOnOrAfter");
                innerText = attr.Value;
            }

            if (string.IsNullOrEmpty(innerText))
            {
                _tokenExpireDate = defaultExpireDate;
            }

            if (DateTime.TryParse(innerText, out defaultExpireDate))
            { // && !innerText.EndsWith("Z")
                _tokenExpireDate = defaultExpireDate.ToUniversalTime();
            }
        }

        private string GetUrl(string url)
        {
            if (url.Contains("?"))
            {
                if (!String.IsNullOrEmpty(this.RelyingParty))
                {
                    url += "&realm=" + HttpUtility.UrlEncode(this.RelyingParty);
                }
                if (!String.IsNullOrEmpty(this.IdentityProvider))
                {
                    url += "&homeRealm=" + HttpUtility.UrlEncode(this.IdentityProvider);
                }
                return url;
            }
            else
            {
                url = (url.EndsWith("/") ? url : url + "/");
                if (!String.IsNullOrEmpty(this.RelyingParty))
                {
                    url += "?realm=" + HttpUtility.UrlEncode(this.RelyingParty);
                }
                if (!String.IsNullOrEmpty(this.IdentityProvider))
                {
                    url += (url.Contains("?") ? "&" : "?") + "homeRealm=" + HttpUtility.UrlEncode(this.IdentityProvider);
                }
                return url;
            }

        }

        private string GetCredentialHost(string uri)
        {
            WebRequest request = HttpWebRequest.Create(uri);
            request.UseDefaultCredentials = true;
            WebResponse response = request.GetResponse();
            var host = response.ResponseUri.Scheme + "://" + response.ResponseUri.Host;
            response.Close();
            request.Abort();
            return host;
        }


    }
}
