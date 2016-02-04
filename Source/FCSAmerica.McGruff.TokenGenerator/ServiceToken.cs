﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml;

namespace FCSAmerica.McGruff.TokenGenerator
{

        public class ServiceToken
        {
            private string _credentialUri;
            private static object _tokenLock = new object();
            private static object _auditInfoLock = new object();


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

            private DateTime _tokenExpireDate = DateTime.MaxValue;
            private string _token;

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

                    if(_auditInfo == null)
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
                get { return _refreshMinutesBeforeExpire;  }
                set { _refreshMinutesBeforeExpire = value; }
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
                        var request = GetWebRequest(ECSServiceAddress);
                        string json = GetResponseContent(request.GetResponse());
                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        var list = serializer.Deserialize<Dictionary<string, object>>(json);

                        _configItems = new Dictionary<string, string>();
                        var configurationSetList = (ArrayList)list["ConfigurationList"];
                        foreach (var configurationSet in configurationSetList)
                        {
                            var configurations = (ArrayList)((Dictionary<string, object>)configurationSet)["ConfigurationSettings"];
                            foreach (var configuration in configurations)
                            {
                                var configEntry = (Dictionary<string, object>)configuration;

                                if (!_configItems.ContainsKey(configEntry["Key"].ToString()))
                                {
                                    _configItems.Add(configEntry["Key"].ToString(), configEntry["Value"].ToString());
                                }

                            }
                        }

                    }
                    return _configItems;
                }
            }



            private HttpWebRequest _request;
            public WebRequest GetWebRequest(string uri)
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.AllowAutoRedirect = true;

                cookieContainer = cookieContainer ?? new CookieContainer();
                request.CookieContainer = cookieContainer;

                if (NetworkCredential != null)
                {
                    var credentialCache = new CredentialCache();
                    if (String.IsNullOrEmpty(_credentialUri))
                    {
                        // _credentialUri should be something like:  https://fs.fcsamerica.com
                        _credentialUri = GetCredentialHost(uri);
                    }

                    credentialCache.Add(new Uri(_credentialUri), "Negotiate", NetworkCredential);
                    credentialCache.Add(new Uri(_credentialUri), "NTLM", NetworkCredential);
                    credentialCache.Add(new Uri(_credentialUri), "Forms", NetworkCredential);
                  
                     request.Credentials = credentialCache;
                }
                else
                {
                    request.UseDefaultCredentials = true;
                }

                request.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)";
                request.Accept = "*/*";
                return request;
            }


            private void RefreshToken()
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

                _token = GetSTSTokenFromIdpToken(stsUrl, idpToken, true);

                SetExpireDateFromToken();
            }


            private string RefreshAuditInfo()
            {
                var request = GetWebRequest(this.AuditInfoServiceEndpoint);
                request.Headers.Add("Authorization", "SAML " + this.Token);

                string auditInfo = GetResponseContent(request.GetResponse());

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

                var stsToken = GetTokenFromBody(stsBody);

                if (cleanToken)
                {
                    // need to clean token.
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(stsToken);
                    var node = doc.SelectSingleNode("//*[local-name()='Assertion']");
                    if (node == null)
                    {
                        _token = null;
                    }

                    return node.OuterXml;
                }
                return stsToken;
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
                    return _tokenExpireDate.Subtract(new TimeSpan(0,0,_refreshMinutesBeforeExpire,0)) < DateTime.UtcNow;
                }
            }

            private void SetExpireDateFromToken()
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
                var host =  response.ResponseUri.Scheme + "://" + response.ResponseUri.Host;
                return host;
            }

        }

}
