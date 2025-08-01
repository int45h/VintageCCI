using System;

using System.Net.Http;
using System.Net.Http.Headers;

using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using System.Linq;

public class TwitchEventBus
{
    public ClientWebSocket Socket;
    private bool m_ready = false;

    private byte[] m_buffer = new byte[65536];

    public Action<string, string> ChatCallback;

    public TwitchEventBus()
    {
        Socket = new ClientWebSocket();
    }

    // https://stackoverflow.com/questions/68283782/how-can-i-set-listener-for-clientwebsocket-in-c
    public async Task EstablishSocketConnection()
    {
        if (Socket.State != WebSocketState.Open)
            Socket = new ClientWebSocket();

        await Socket.ConnectAsync(new Uri("wss://eventsub.wss.twitch.tv/ws"), CancellationToken.None);
        if (m_ready)
            await SubscribeEvents();
        await Task.WhenAll(SocketReceive());
    }

    private async Task SocketReceive()
    {
        while (Socket.State == WebSocketState.Open)
        {
            var res = await Socket.ReceiveAsync(new ArraySegment<byte>(m_buffer, 0, m_buffer.Count()), CancellationToken.None);
            if (res.MessageType == WebSocketMessageType.Close)
            {
                await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                if (m_ready)
                {
                    await EstablishSocketConnection();
                    continue;
                }
            }

            string msg = System.Text.Encoding.UTF8.GetString(m_buffer, 0, res.Count);
            await HandleMessages(msg);
            
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }

    private async Task HandleMessages(string msg)
    {
        JObject jmsg = JObject.Parse(msg);
        string msg_type = jmsg["metadata"]["message_type"].ToString();
        switch (msg_type)
        {
            case "session_welcome":
                TwitchInterface.Context.SessionID = jmsg["payload"]["session"]["id"].ToString();
                await SubscribeEvents();
            break;
            case "notification":
                await HandleEvents(jmsg);
            break; 
        }
    }

    private async Task HandleEvents(JObject jmsg)
    {
        string type = jmsg["payload"]["subscription"]["type"].ToString();
        switch (type)
        {
            case "channel.chat.message":
            {
                JToken ev = jmsg["payload"]["event"];
                ChatCallback?.Invoke(ev["chatter_user_name"].ToString(), ev["message"]["text"].ToString());
            }
            break;
        }
        return;
    }

    private async Task SubscribeEvents()
    {
        await SubscribeToChatMessages();
    }

    public void Reset()
    {
        // TO-DO: make this do a proper reset
        m_ready = false;
    }

    // Subscribe to the chat messages event 
    public async Task SubscribeToChatMessages()
    {
        if (Socket.State != WebSocketState.Open)
            return;

        JObject j = JObject.Parse("{'type': 'channel.chat.message','version': '1','condition': {'broadcaster_user_id': '"+TwitchInterface.Context.UserID+"', 'user_id': '"+TwitchInterface.Context.UserID+"'},'transport': {'method': 'websocket','session_id': '"+TwitchInterface.Context.SessionID+"'}}");

        using var req = new HttpRequestMessage();
        req.RequestUri = new Uri(TwitchInterface.BaseUri + "/eventsub/subscriptions");
        req.Method = HttpMethod.Post;
        req.Headers.Add("Client-Id", TwitchInterface.Context.TwitchID);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TwitchInterface.Context.TwitchOAuthKey);
        
        req.Content = new StringContent(j.ToString(), new MediaTypeHeaderValue("application/json"));
        using HttpResponseMessage msg = await TwitchInterface.SendAsync(req);
        
        if (msg.StatusCode == System.Net.HttpStatusCode.OK || msg.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            string result = await msg.Content.ReadAsStringAsync();
            m_ready = true; // Ready to parse messages
        }
        else
        {
            string result = await msg.Content.ReadAsStringAsync();
        }
    }
}