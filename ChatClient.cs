using System;
using Gecko;
using System.Diagnostics;
using System.Windows.Threading;
using System.Windows.Forms;
using WebSocket4Net;
using System.Net;

namespace WSpa
{
    public enum ProxyType
    {
        none,
        http,
        socks4,
        socks4a,
        socks5
    }

    public class ProxyInformation
    {
        public ProxyType Which { get; set; }
        public int Port { get; set; }
        public string Address { get; set; }

        public ProxyInformation()
        {
            Which = ProxyType.none;
            Address = string.Empty;
            Port = 0;

            string[] cmd_params = Environment.GetCommandLineArgs();
            for(int i = 0; i < cmd_params.Length; ++i)
            {
                switch(cmd_params[i])
                {
                    case "-t":
                        {
                            int j = i + 1;
                            if (j < cmd_params.Length)
                            {
                                switch (cmd_params[j])
                                {
                                    case "socks4a":
                                        Which = ProxyType.socks4a;
                                        break;
                                    case "socks4":
                                        Which = ProxyType.socks4;
                                        break;
                                    case "socks5":
                                        Which = ProxyType.socks5;
                                        break;
                                    case "http":
                                        Which = ProxyType.http;
                                        break;
                                }
                            }
                        }
                        break;

                    case "-h":
                        {
                            int j = i + 1;
                            if (j < cmd_params.Length)
                            {
                                string[] splitted = cmd_params[j].Split(':');
                                Address = splitted[0];
                                if(splitted.Length == 2)
                                {
                                    int tempPort = 0;
                                    if (Int32.TryParse(splitted[1], out tempPort))
                                    {
                                        Port = tempPort;
                                    }
                                }
                            }
                        }
                        break;

                    case "-p":
                        {
                            int j = i + 1;
                            if (j < cmd_params.Length)
                            {
                                int tempPort = 0;
                                if(Int32.TryParse(cmd_params[j], out tempPort))
                                {
                                    Port = tempPort;
                                }
                            }
                        }
                        break;
                }
            }
        }

        public override string ToString()
        {
            return "Proxy: (" + Which + ") " + Address + ":" + Port;
        }
    }

    public class ChatClient
    {
        public enum State
        {
            GeneratingSID,
            Connecting,
            Idle,
            Searching,
            Chatting,
            Error,
            Disconnected
        }

        //
        public delegate void OnChatConnectDelegate(ChatClient cc);
        public event OnChatConnectDelegate OnChatConnect;
        public delegate void OnChatSearchDelegate(ChatClient cc);
        public event OnChatSearchDelegate OnChatSearch;
        public delegate void OnChatBeginDelegate(ChatClient cc);
        public event OnChatBeginDelegate OnChatBegin;
        public delegate void OnChatTypingDelegate(ChatClient cc, bool me, bool typing);
        public event OnChatTypingDelegate OnChatTyping;
        public delegate void OnChatMessageDelegate(ChatClient cc, bool me, string message);
        public event OnChatMessageDelegate OnChatMessage;
        public delegate void OnChatEndDelegate(ChatClient cc);
        public event OnChatEndDelegate OnChatEnd;
        public delegate void OnChatDisconnectDelegate(ChatClient cc, EventArgs e);
        public event OnChatDisconnectDelegate OnChatDisconnect;
        public delegate void OnChatOnlineCountDelegate(ChatClient cc, int onlineCount);
        public event OnChatOnlineCountDelegate OnChatOnlineCount;
        public delegate void OnChatErrorDelegate(ChatClient cc, SuperSocket.ClientEngine.ErrorEventArgs e);
        public event OnChatErrorDelegate OnChatError;

        public delegate void OnNewSIDGeneratedDelegate(ChatClient cc, WebSocketSIDGenerator generator, string SID);
        public event OnNewSIDGeneratedDelegate OnNewSIDGenerated;

        //

        private WebSocketSIDGenerator _generator;
        private WebSocket _socket;
        private Stopwatch _pinger;
        private Stopwatch _online_count_updater;
        private Stopwatch _search_timer;
        private Stopwatch _time_since_last_msg;
        private Timer _keep_alive_timer;
        private ProxyInformation proxyInformation;

        public GeckoWebBrowser GetControl { get { return _generator.GetControl; } }

        public int OnlineCount { get; private set; }
        private bool connected;
        private bool searching;
        private bool chatting;
        private bool typing;
        private bool otherTyping;
        private bool errord;
        private bool errore;
        private bool sockopen;
        private Dispatcher _dispatcher;

        public ChatClient(ProxyInformation pi, Dispatcher dispatcher = null)
        {
            Stop();

            _dispatcher = dispatcher;
            proxyInformation = pi;

            _socket = null;
            _generator = new WebSocketSIDGenerator();
            _generator.OnNewSIDGenerated += Generator_OnNewSIDGenerated;
            _generator.GenerateSid(proxyInformation);

            errord = false;
            errore = false;

            _keep_alive_timer = new Timer();
            _keep_alive_timer.Interval = 1000;
            _keep_alive_timer.Tick += _keep_alive_timer_Tick;
            _keep_alive_timer.Start();
        }

        private void _keep_alive_timer_Tick(object sender, EventArgs e)
        {
            KeepAliveTick();
        }

        private void Generator_OnNewSIDGenerated(WebSocketSIDGenerator generator, string SID)
        {
            if(OnNewSIDGenerated != null)
            {
                OnNewSIDGenerated(this, generator, SID);
            }

            if (_socket == null)
            {
                if (SID != string.Empty)
                {
                    Stop();

                    _pinger = new Stopwatch();
                    _online_count_updater = new Stopwatch();
                    _search_timer = new Stopwatch();
                    _time_since_last_msg = new Stopwatch();

                    _socket = new WebSocket("ws://ws.praatanoniem.nl/socket.io/?EIO=3&transport=websocket&sid=" + SID, origin: "http://ws.praatanoniem.nl");
                    switch (proxyInformation.Which)
                    {
                        case ProxyType.none:
                            break;
                        case ProxyType.http:
                            {
                                // go through proxy for testing
                                var proxy = new SuperSocket.ClientEngine.Proxy.HttpConnectProxy(new IPEndPoint(IPAddress.Parse(proxyInformation.Address), proxyInformation.Port), 16777216);
                                _socket.Proxy = (SuperSocket.ClientEngine.IProxyConnector)proxy;
                            }
                            break;
                        case ProxyType.socks4:
                            {
                                var proxy = new SuperSocket.ClientEngine.Proxy.Socks4Connector(new IPEndPoint(IPAddress.Parse(proxyInformation.Address), proxyInformation.Port), "");
                                _socket.Proxy = (SuperSocket.ClientEngine.IProxyConnector)proxy;
                            }
                            break;
                        case ProxyType.socks4a:
                            {
                                var proxy = new SuperSocket.ClientEngine.Proxy.Socks4aConnector(new IPEndPoint(IPAddress.Parse(proxyInformation.Address), proxyInformation.Port), "");
                                _socket.Proxy = (SuperSocket.ClientEngine.IProxyConnector)proxy;
                            }
                            break;
                        case ProxyType.socks5:
                            {
                                var proxy = new SuperSocket.ClientEngine.Proxy.Socks5Connector(new IPEndPoint(IPAddress.Parse(proxyInformation.Address), proxyInformation.Port));
                                _socket.Proxy = (SuperSocket.ClientEngine.IProxyConnector)proxy;
                            }
                            break;
                    }

                    _socket.Closed += _socket_Closed;
                    _socket.Error += _socket_Error;
                    _socket.MessageReceived += _socket_MessageReceived;
                    _socket.Opened += _socket_Opened;

                    _socket.Open();
                }
                else
                {
                    _socket_Error(null, null);
                }
            }
        }

        private void KeepAliveTick()
        {
            if(connected)
            {
                if(_pinger.ElapsedMilliseconds > 30000)
                {
                    _pinger.Restart();
                    _socket.Send("2");
                }
                if(_online_count_updater.ElapsedMilliseconds > 15133)
                {
                    _online_count_updater.Restart();
                    _socket.Send("42[\"count\"]");
                }
            }
        }

        public bool IsSearching()
        {
            return searching;
        }

        public double GetElapsedSearchTime()
        {
            if(_search_timer == null)
            {
                return 0.0;
            }

            return _search_timer.ElapsedMilliseconds;
        }

        public bool CanSearch()
        {
            return connected && !chatting && !searching;
        }

        public bool Search()
        {
            if(!connected || searching || chatting)
            {
                return false;
            }
            _socket.Send("42[\"start\"]");
            searching = true;
            _search_timer.Restart();

            if (_dispatcher != null)
            {
                _dispatcher.BeginInvoke((OnChatSearchDelegate)((ChatClient cc) =>
                {
                    if (OnChatSearch != null)
                    {
                        OnChatSearch(cc);
                    }
                }), this);
            }

            return true;
        }

        public bool IsTyping()
        {
            return typing;
        }

        public bool IsOtherTyping()
        {
            return otherTyping;
        }

        public bool SetTyping(bool isTyping)
        {
            if(!connected || !chatting || searching || isTyping == typing)
            {
                return false;
            }

            typing = isTyping;

            _socket.Send("42[\"typing\"]{\"typing\":" + (typing ? "true" : "false") + "}]");

            if (_dispatcher != null)
            {
                _dispatcher.BeginInvoke((OnChatTypingDelegate)((ChatClient cc, bool m, bool t) =>
                {
                    if (OnChatTyping != null)
                    {
                        OnChatTyping(cc, m, t);
                    }
                }), this, true, typing);
            }

            return true;
        }

        public bool IsChatting()
        {
            return chatting;
        }

        public bool SendMessage(string message)
        {
            if (!connected || !chatting || searching || string.IsNullOrEmpty(message))
            {
                return false;
            }

            _socket.Send("42[\"message\"]{\"message\":\"" + message + "\"}]");
            _time_since_last_msg.Restart();

            if (_dispatcher != null)
            {
                _dispatcher.BeginInvoke((OnChatMessageDelegate)((ChatClient cc, bool m, string msg) =>
                {
                    if (OnChatMessage != null)
                    {
                        OnChatMessage(cc, m, msg);
                    }
                }), this, true, message);
            }

            return true;
        }

        public double GetElapsedLastMessageTime(double default_return_value = 0.0)
        {
            if(_time_since_last_msg == null)
            {
                return default_return_value;
            }

            return _time_since_last_msg.ElapsedMilliseconds;
        }

        private void _socket_Opened(object sender, EventArgs e)
        {
            _pinger.Start();
            _online_count_updater.Start();

            _socket.Send("2probe");

            sockopen = true;
        }

        private void _socket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            string incomming_message = e.Message;
            string json = string.Empty;
            if(incomming_message.StartsWith("42["))
            {
                incomming_message = incomming_message.Substring(2);
                int start_brace = incomming_message.IndexOf("\",{");
                if (start_brace != -1)
                {
                    start_brace += 2;
                    if(incomming_message.IndexOf("}]", start_brace) != -1)
                    {
                        json = incomming_message.Substring(start_brace, incomming_message.Length - (start_brace + 1));
                    }
                }
            }

            if (json == string.Empty)
            {
                if (searching && incomming_message == "[\"start\"]")
                {
                    searching = false;
                    chatting = true;
                    typing = false;
                    otherTyping = false;

                    double elapsed = _search_timer.ElapsedMilliseconds;
                    _search_timer.Reset();

                    if (_dispatcher != null)
                    {
                        _dispatcher.BeginInvoke((OnChatBeginDelegate)((ChatClient cc) =>
                        {
                            if (OnChatBegin != null)
                            {
                                OnChatBegin(cc);
                            }
                        }), this);
                    }
                }
                else if (chatting && incomming_message == "[\"end\"]")
                {
                    searching = false;
                    chatting = false;
                    typing = false;
                    otherTyping = false;

                    if (_dispatcher != null)
                    {
                        _dispatcher.BeginInvoke((OnChatEndDelegate)((ChatClient cc) =>
                        {
                            if (OnChatEnd != null)
                            {
                                OnChatEnd(cc);
                            }
                        }), this);
                    }
                }
                else if(sockopen && !connected && incomming_message == "3probe")
                {
                    _socket.Send("5");

                    connected = true;

                    if (_dispatcher != null)
                    {
                        _dispatcher.BeginInvoke((OnChatConnectDelegate)((ChatClient cc) =>
                        {
                            if (OnChatConnect != null)
                            {
                                OnChatConnect(cc);
                            }
                        }), this);
                    }
                }
            }
            else if(json.Length > 2)
            {
                switch(json[2])
                {
                    case 't': //{"typing":true}
                        bool temp = json[json.Length - 3] == 'u';
                        if(temp != otherTyping)
                        {
                            otherTyping = temp;
                            if (_dispatcher != null)
                            {
                                _dispatcher.BeginInvoke((OnChatTypingDelegate)((ChatClient cc, bool m, bool t) =>
                                {
                                    if (OnChatTyping != null)
                                    {
                                        OnChatTyping(cc, m, t);
                                    }
                                }), this, false, otherTyping);
                            }
                        }
                        break;
                    case 'u': //{"userCount":123}
                        int integer_start = json.IndexOf(':') + 1;
                        int temp_val;
                        if (Int32.TryParse(json.Substring(integer_start, json.Length - (integer_start + 1)), out temp_val))
                        {
                            OnlineCount = temp_val;
                            if (_dispatcher != null)
                            {
                                _dispatcher.BeginInvoke((OnChatOnlineCountDelegate)((ChatClient cc, int c) =>
                                {
                                    if (OnChatOnlineCount != null)
                                    {
                                        OnChatOnlineCount(cc, c);
                                    }
                                }), this, OnlineCount);
                            }
                        }
                        break;
                    case 'm': //{"message":"hello world!","sender":"Onbekende"}
                        int start_message = json.IndexOf("e\":\"") + 4;
                        int end_message = json.LastIndexOf("\",\"s");

                        string recvd_message = json.Substring(start_message, end_message - start_message);
                        _time_since_last_msg.Restart();

                        if (_dispatcher != null)
                        {
                            _dispatcher.BeginInvoke((OnChatMessageDelegate)((ChatClient cc, bool m, string msg) =>
                            {
                                if (OnChatMessage != null)
                                {
                                    OnChatMessage(cc, m, msg);
                                }
                            }), this, false, recvd_message);
                        }
                        break;
                }          
            }
        }

        private void _socket_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            errore = true;
            if (_dispatcher != null)
            {
                _dispatcher.BeginInvoke((OnChatErrorDelegate)((ChatClient cc, SuperSocket.ClientEngine.ErrorEventArgs _e) =>
                {
                    if (OnChatError != null)
                    {
                        OnChatError(cc, _e);
                    }
                }), this, e);
            }
            Stop();
        }

        private void _socket_Closed(object sender, EventArgs e)
        {
            errord = true;
            if (_dispatcher != null)
            {
                _dispatcher.BeginInvoke((OnChatDisconnectDelegate)((ChatClient cc, EventArgs _e) =>
                {
                    if (OnChatDisconnect != null)
                    {
                        OnChatDisconnect(cc, _e);
                    }
                }), this, e);
            }
            Stop();
        }

        private void Stop()
        {
            _socket = null;

            chatting = false;
            searching = false;
            connected = false;
            typing = false;
            otherTyping = false;
            sockopen = false;
            OnlineCount = 0;

            if (_pinger != null)
            {
                _pinger = null;
            }
            if (_online_count_updater != null)
            {
                _online_count_updater = null;
            }
            if(_search_timer != null)
            {
                _search_timer = null;
            }
            if(_time_since_last_msg != null)
            {
                _time_since_last_msg = null;
            }
        }

        public State GetState()
        {
            if(errore)
            {
                return State.Error;
            }

            if(errord)
            {
                return State.Disconnected;
            }

            if(_socket == null)
            {
                return State.GeneratingSID;
            }

            if(!connected)
            {
                return State.Connecting;
            }

            if(IsSearching())
            {
                return State.Searching;
            }

            if(IsChatting())
            {
                return State.Chatting;
            }

            return State.Idle;
        }
    }
}
