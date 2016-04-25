using Gecko;
using System.Web;
using System.Windows.Forms;

namespace WSpa
{
    public class WebSocketSIDGenerator
    {
        public delegate void OnNewSIDGeneratedDelegate(WebSocketSIDGenerator generator, string SID);
        public event OnNewSIDGeneratedDelegate OnNewSIDGenerated;

        private GeckoWebBrowser browser;
        private bool generating;
        private bool cancelAll;

        public GeckoWebBrowser GetControl { get { return browser; } }

        public WebSocketSIDGenerator()
        {
            Gecko.Xpcom.Initialize("..\\..\\xulrunner");
            browser = new GeckoWebBrowser();

            
            browser.CreateWindow += Browser_CreateWindow;
            browser.NavigationError += Browser_NavigationError;
            browser.ObserveHttpModifyRequest += Browser_ObserveHttpModifyRequest;
            browser.DocumentCompleted += Browser_DocumentCompleted;
            browser.UseHttpActivityObserver = true;
            cancelAll = false;
            generating = false;


        }

        private void Browser_DocumentCompleted(object sender, Gecko.Events.GeckoDocumentCompletedEventArgs e)
        {
            if (generating)
            {
                if (browser.Document.GetElementById("chatbox") == null)
                {
                    generating = false;
                    browser.Stop();
                    //browser.LoadHtml("");
                    
                    OnNewSIDGenerated(this, string.Empty);
                }
            }
        }

        public void SetProxy(ProxyInformation information = null)
        {
            if (information != null)
            {
                switch(information.Which)
                {
                    case ProxyType.none:
                        {
                            GeckoPreferences.Default["network.proxy.type"] = 0;
                            GeckoPreferences.Default["network.proxy.http"] = "";
                            GeckoPreferences.Default["network.proxy.http_port"] = 0;
                            GeckoPreferences.Default["network.proxy.socks"] = "";
                            GeckoPreferences.Default["network.proxy.socks_port"] = 0;
                            GeckoPreferences.Default["network.proxy.socks_version"] = 0;
                        }
                        break;
                    case ProxyType.http:
                        {
                            GeckoPreferences.Default["network.proxy.type"] = 1;
                            GeckoPreferences.Default["network.proxy.http"] = information.Address;
                            GeckoPreferences.Default["network.proxy.http_port"] = information.Port;
                            GeckoPreferences.Default["network.proxy.socks"] = "";
                            GeckoPreferences.Default["network.proxy.socks_port"] = 0;
                            GeckoPreferences.Default["network.proxy.socks_version"] = 0;
                        }
                        break;
                    case ProxyType.socks4:
                    case ProxyType.socks4a:
                        {
                            GeckoPreferences.Default["network.proxy.type"] = 1;
                            GeckoPreferences.Default["network.proxy.http"] = "";
                            GeckoPreferences.Default["network.proxy.http_port"] = 0;
                            GeckoPreferences.Default["network.proxy.socks"] = information.Address;
                            GeckoPreferences.Default["network.proxy.socks_port"] = information.Port;
                            GeckoPreferences.Default["network.proxy.socks_version"] = 4;
                        }
                        break;
                    case ProxyType.socks5:
                        {
                            GeckoPreferences.Default["network.proxy.type"] = 1;
                            GeckoPreferences.Default["network.proxy.http"] = "";
                            GeckoPreferences.Default["network.proxy.http_port"] = 0;
                            GeckoPreferences.Default["network.proxy.socks"] = information.Address;
                            GeckoPreferences.Default["network.proxy.socks_port"] = information.Port;
                            GeckoPreferences.Default["network.proxy.socks_version"] = 5;
                        }
                        break;
                }

                GeckoPreferences.Default["browser.xul.error_pages.enabled"] = true;
                GeckoPreferences.User["network.proxy.type"] = GeckoPreferences.Default["network.proxy.type"];
                GeckoPreferences.User["network.proxy.http"] = GeckoPreferences.Default["network.proxy.http"];
                GeckoPreferences.User["network.proxy.http_port"] = GeckoPreferences.Default["network.proxy.http_port"];
                GeckoPreferences.User["network.proxy.socks"] = GeckoPreferences.Default["network.proxy.socks"];
                GeckoPreferences.User["network.proxy.socks_port"] = GeckoPreferences.Default["network.proxy.socks_port"];
                GeckoPreferences.User["network.proxy.socks_version"] = GeckoPreferences.Default["network.proxy.socks_version"];
                GeckoPreferences.User["browser.xul.error_pages.enabled"] = GeckoPreferences.Default["browser.xul.error_pages.enabled"];

            }
        }

        private bool ContainsAny(string haystack, params string[] needles)
        {
            foreach (string needle in needles)
            {
                if (haystack.Contains(needle))
                    return true;
            }

            return false;
        }

        private void Browser_ObserveHttpModifyRequest(object sender, GeckoObserveHttpModifyRequestEventArgs e)
        {
            if (cancelAll)
            {
                e.Cancel = true;
            }
            else
            {
                if (ContainsAny(e.Uri.ToString(), 
                    ".jpg", ".css", ".png", ".gif", 
                    ".mp4", ".mp3", ".svg", ".ico", 
                    ".tif", "image", "googleadservices.com",
                    "doubleclick.net", "googlesyndication.com", 
                    "cdn.chatid.nl", "google-analytics.com"))
                {
                    e.Cancel = true;
                }
                else if (generating)
                {
                    var query = HttpUtility.ParseQueryString(e.Uri.Query);
                    if (query.HasKeys() && query.GetValues("transport") != null && query.GetValues("transport")[0].CompareTo("websocket") == 0)
                    {
                        string sid = query.GetValues("sid")[0];

                        e.Cancel = true;
                        cancelAll = true;
                        generating = false;
                        browser.Stop();
                        //browser.LoadHtml("");//this is important haha

                        if (OnNewSIDGenerated != null)
                        {
                            OnNewSIDGenerated(this, sid);
                        }
                    }
                }
            }
        }

        private void Browser_NavigationError(object sender, Gecko.Events.GeckoNavigationErrorEventArgs e)
        {
            if (generating)
            {
                generating = false;
                cancelAll = false;
                browser.Stop();
                //browser.LoadHtml("");

                OnNewSIDGenerated(this, string.Empty);
            }
        }

        private void Browser_CreateWindow(object sender, GeckoCreateWindowEventArgs e)
        {
            e.Cancel = true;
        }

        public bool GenerateSid(ProxyInformation information = null)
        {
            if(generating)
            {
                return false;
            }

            generating = true;
            cancelAll = false;
            SetProxy(information);
            browser.Navigate("http://www.praatanoniem.nl/");
            //browser.Navigate("http://ip.gz0.nl/");
            return true;
        }

        public bool GeneratingInProgress()
        {
            return generating;
        }
    }
}
