using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace FCSAmerica.McGruff.TokenGenerator.BrowserBased
{
    public class BrowserTokenRetriever : IDisposable
    {
        private readonly TraceSource _traceSource = new TraceSource("FCSAmerica.McGruff.TokenGenerator");

        private const int MaxRedirectLoopCount = 10;

        private readonly string _authenticationUrl;
        private readonly string _issuingAuthority;

        private int _browserRedirectCount;

        private readonly WebBrowser _browser;

        private string _stsToken;


        public BrowserTokenRetriever(string authenticationUrl, string issuingAuthority)
        {
            _authenticationUrl = authenticationUrl;
            _issuingAuthority = issuingAuthority;

            _browser = new WebBrowser();
            _browser.Visible = false;
            _browser.AllowNavigation = true;
            _browser.Navigated += _browser_Navigated;
            _browser.DocumentCompleted += _browser_DocumentCompleted;
            _browser.ScriptErrorsSuppressed = true;
        }

        public string RetrieveToken()
        {
            _browser.Navigate(_authenticationUrl);
            Application.Run();
            return _stsToken;
        }

        void _browser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            try
            {
                _browserRedirectCount++;

                var currentBrowser = (sender as WebBrowser);
                if (currentBrowser == null)
                {
                    StartExitingThreadWithError(new Exception("WebBrowser is null."));
                    return;
                }

                var document = currentBrowser.Document;

                if (document == null)
                {
                    StartExitingThreadWithError(new Exception("document is null."));
                    return;
                }
                _traceSource.TraceInformation("\nBrowser_DocumentComplete Count: " + _browserRedirectCount);

                //_traceSource.TraceInformation("\n\nHtml:\n{0}\n", (document.body == null) ? document.toString() : document.body.innerHTML);

                if (document.ActiveElement != null)
                {
                    var wsResultElement = document.All.GetElementsByName("wresult");
                    if (wsResultElement.Count > 0)
                    {

                        var wsResultValue = wsResultElement[0].GetAttribute("value");

                        if (!string.IsNullOrEmpty(wsResultValue))
                        {
                            var issuerRedirectUrlSearchText = string.Format("<Issuer>{0}</Issuer>", _issuingAuthority);

                            if (wsResultValue.IndexOf(issuerRedirectUrlSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // stop we have the token.
                                var stsToken = wsResultValue;
                                StartExitingThreadWithToken(stsToken);
                            }

                        }
                        else
                        {
                            TraceHtml("wresult value is empty.", document);
                        }
                    }
                    else
                    {
                        TraceHtml("wresult element not found.", document);
                    }
                }

                if (_browserRedirectCount == MaxRedirectLoopCount)
                {
                    _traceSource.TraceInformation("\nMaxRedirectLoopCount reached. Stopping browser and returning back empty token.");
                    StartExitingThreadWithToken(string.Empty);
                }
            }
            catch (Exception ex)
            {
                StartExitingThreadWithError(ex);
            }
        }

        private void TraceHtml(string message, HtmlDocument document)
        {
            var documentHtml = (document.Body == null) ? document.ToString() : document.Body.InnerHtml;
            _traceSource.TraceEvent(TraceEventType.Warning, 0, "\n" + message + " Document Html:\n " + documentHtml);
        }

        void _browser_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            _traceSource.TraceInformation("\nBrowser Navigation completed to url: {0}", e.Url);
        }

        private void StartExitingThreadWithToken(string token)
        {
            _stsToken = token;
            Application.ExitThread();
        }

        private void StartExitingThreadWithError(Exception ex)
        {
            _traceSource.TraceEvent(TraceEventType.Error, 0, "Error Occured during DocumentComplete.\n{0}", ex.ToString());
            Application.ExitThread();
        }

        public void Dispose()
        {
            if (_browser != null)
            {
                _browser.Dispose();
            }
        }
    }
}
