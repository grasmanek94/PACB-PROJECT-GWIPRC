using System;
using System.Diagnostics;
using System.Net;
using System.Windows.Forms;
using System.Windows.Threading;

namespace WSpa
{
    public partial class Form1 : Form
    {
        private ChatClient client;
        private Stopwatch stopwatch;
        private Dispatcher UIDispatcher;
        private ProxyInformation proxyInformation;

        public Form1()
        {
            InitializeComponent();

            UIDispatcher = Dispatcher.CurrentDispatcher;

            stopwatch = new Stopwatch();
            stopwatch.Restart();

            proxyInformation = new ProxyInformation();
            this.Text = proxyInformation.ToString();

            client = new ChatClient(proxyInformation, UIDispatcher);
     
            client.OnChatBegin += Client_OnChatBegin;
            client.OnChatConnect += Client_OnChatConnect;
            client.OnChatDisconnect += Client_OnChatDisconnect;
            client.OnChatEnd += Client_OnChatEnd;
            client.OnChatError += Client_OnChatError;
            client.OnChatMessage += Client_OnChatMessage;
            client.OnChatOnlineCount += Client_OnChatOnlineCount;
            client.OnChatSearch += Client_OnChatSearch;
            client.OnChatTyping += Client_OnChatTyping;
            client.OnNewSIDGenerated += Client_OnNewSIDGenerated;

            client.GetControl.Dock = DockStyle.Fill;
            this.Controls.Add(client.GetControl);

            client.GetControl.ProgressChanged += GetControl_ProgressChanged;
            client.GetControl.RequestProgressChanged += GetControl_RequestProgressChanged;
        }

        private void GetControl_RequestProgressChanged(object sender, Gecko.GeckoRequestProgressEventArgs e)
        {
            label11.Text = e.CurrentProgress + "/" + e.MaximumProgress;
        }

        private void GetControl_ProgressChanged(object sender, Gecko.GeckoProgressEventArgs e)
        {
            label12.Text = e.CurrentProgress + "/" + e.MaximumProgress;
        }

        private void Client_OnNewSIDGenerated(ChatClient cc, WebSocketSIDGenerator generator, string SID)
        {
            label10.Text = stopwatch.Elapsed.TotalSeconds + " | Client_OnNewSIDGenerated(" + SID + ")";
        }

        private void Client_OnChatTyping(ChatClient cc, bool me, bool typing)
        {
            label1.Text = stopwatch.Elapsed.TotalSeconds + " | Client_OnChatTyping(" + me + ", " + typing + ")";
        }

        private void Client_OnChatSearch(ChatClient cc)
        {
            label2.Text = stopwatch.Elapsed.TotalSeconds + " | Client_OnChatSearch()";
        }

        private void Client_OnChatOnlineCount(ChatClient cc, int onlineCount)
        {
            label3.Text = stopwatch.Elapsed.TotalSeconds + " | Client_OnChatOnlineCount(" + onlineCount + ")";
        }

        private void Client_OnChatMessage(ChatClient cc, bool me, string message)
        {
            label4.Text = stopwatch.Elapsed.TotalSeconds + " | Client_OnChatMessage(" + me + ", " + message + ")";
        }

        private void Client_OnChatError(ChatClient cc, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            label5.Text = stopwatch.Elapsed.TotalSeconds + " | Client_OnChatError()";
            if(e != null)
            {
                label5.Text += ": " + e.ToString();
                if (e.Exception != null)
                {
                    label5.Text += ": " + e.Exception.ToString();
                }
            }
        }

        private void Client_OnChatEnd(ChatClient cc)
        {
            label6.Text = stopwatch.Elapsed.TotalSeconds + " | Client_OnChatEnd()";
        }

        private void Client_OnChatDisconnect(ChatClient cc, EventArgs e)
        {
            label7.Text = stopwatch.Elapsed.TotalSeconds + " | Client_OnChatDisconnect()";
            if(e != null)
            {
                label7.Text += ": " + e.ToString();
            }
        }

        private void Client_OnChatConnect(ChatClient cc)
        {
            label8.Text = stopwatch.Elapsed.TotalSeconds + " | Client_OnChatConnect()";
            cc.Search();
        }

        private void Client_OnChatBegin(ChatClient cc)
        {
            label9.Text = stopwatch.Elapsed.TotalSeconds + " | Client_OnChatBegin()";
        }
    }
}
