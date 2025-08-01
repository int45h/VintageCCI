using System;

using System.Net.Http;
using System.Net.Http.Headers;

using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

// Singleton class that represents the interface between Twitch and our game. 
public class TwitchContext
{
    // TO-DO: don't hardcode your api keys directly in your app.
    public string TwitchID = "twstayj52shk8h1dncwarhl03qm3ug"; // UGUU Smart TV Android 4.4 KitKat Wifi Bluetooth Enabled Full HD 1080p 4k Roku Google Play
    public string RefreshToken = "";
    public string TwitchOAuthKey = ""; 
    public string DeviceCode = "";

    public string Username = ""; // replace this and prompt the user for their username 
    
    public string UserID = "";
    public string SessionID = "";
    
    public bool ConnectionSuccess = false;

    public TwitchContext(){}
}

public class TwitchInterface
{
    private TwitchContext m_twitchContext;
    private string m_userCode = ""; // Display this to the user

    private static TwitchInterface m_interface;
    public static TwitchInterface Instance { get => m_interface; }
    public static TwitchContext Context { get => m_interface.m_twitchContext; }
    private HttpClient m_client;
    private TwitchEventBus m_eventBus;

    public const string BaseUri = "https://api.twitch.tv/helix";

    public string TwitchID { set => Context.TwitchID = value; }

    public TwitchInterface()
    {
        if (m_interface == null)
            m_interface = this;

        m_twitchContext = new TwitchContext();
        m_client = new HttpClient();
        m_eventBus = new TwitchEventBus();
    }

    public static void SubscribeChatEvent(Action<string, string> callback) =>
        Instance.m_eventBus.ChatCallback = callback;

    public static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message)
    {
        return await m_interface.m_client.SendAsync(message);
    }

    public static async void Reset()
    {
        if (m_interface != null && m_interface.m_eventBus != null)
            m_interface.m_eventBus.Reset();
    }

    // Get the OAuth key
    public static async void EstablishConnectionAsync(Action<string> onSuccess, Action<string> onFailure)
    {
        await Instance.DeviceCodeGrantFlowAuth((s) => {
            onSuccess?.Invoke(s);
            InitTwitchEventBus();
        }, onFailure);
    }

    private async Task DeviceCodeGrantFlowAuth(Action<string> onSuccess, Action<string> onFailure)
    {
        using var req = new HttpRequestMessage();
        req.RequestUri = new Uri("https://id.twitch.tv/oauth2/device?"+
            "client_id="+Context.TwitchID+
            "&scopes=user%3Aread%3Achat"
        );
        req.Method = HttpMethod.Post;
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

        using HttpResponseMessage msg = await m_interface.m_client.SendAsync(req);
        if (msg.StatusCode != System.Net.HttpStatusCode.OK)
        {
            string msg_json = await msg.Content.ReadAsStringAsync();
            onFailure?.Invoke(msg_json);
            return;
        }
        
        string json = await msg.Content.ReadAsStringAsync();
        JObject j = JObject.Parse(json);

        Context.DeviceCode = j["device_code"].ToString();
        m_interface.m_userCode = j["user_code"].ToString();
        string auth_uri = j["verification_uri"].ToString();

        // Open a new browser window with the URL to authenticate the user
        System.Diagnostics.Process.Start(new ProcessStartInfo() { 
            FileName = auth_uri,
            UseShellExecute = true
        });

        // Poll for access token
        System.Uri uri = new Uri("https://id.twitch.tv/oauth2/token?" + 
            "client_id="+Context.TwitchID+ 
            "&scopes=user%3Aread%3Achat"+
            "&device_code="+Context.DeviceCode+
            "&grant_type=urn:ietf:params:oauth:grant-type:device_code"
        );
        bool authenticated = false;
        JObject jmsg;
        do
        {
            using HttpRequestMessage req_auth = new HttpRequestMessage();
            req_auth.RequestUri = uri;
            req_auth.Method = HttpMethod.Post;
            req_auth.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

            using var msg_auth = await m_interface.m_client.SendAsync(req_auth);
            jmsg = JObject.Parse(await msg_auth.Content.ReadAsStringAsync());
                
            if (msg_auth.StatusCode != System.Net.HttpStatusCode.OK)
            {
                if (string.Compare(jmsg["message"].ToString(), "authorization_pending") == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1000));
                    continue;
                }
                else
                {
                    onFailure?.Invoke(jmsg.ToString());
                    return;
                }
            }

            authenticated = true;
            Context.TwitchOAuthKey = jmsg["access_token"].ToString();
            Context.RefreshToken = jmsg["refresh_token"].ToString();
        }
        while(!authenticated);
        if (!authenticated) return;
        
        await Instance.GetUserID(Context.Username, onSuccess, onFailure);
    }

    // Get the user ID
    private async Task GetUserID(string username, Action<string> onSuccess, Action<string> onFailure)
    {
        using var req = new HttpRequestMessage();
        req.RequestUri = new Uri(BaseUri+"/users?login="+username);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Context.TwitchOAuthKey);
        req.Headers.Add("Client-Id", Context.TwitchID);

        req.Method = HttpMethod.Get;
        using var msg = await m_interface.m_client.SendAsync(req);
        JObject jmsg = JObject.Parse(await msg.Content.ReadAsStringAsync());

        if (msg.StatusCode == System.Net.HttpStatusCode.OK)
        {
            Context.UserID = jmsg["data"][0]["id"].ToString();
            onSuccess?.Invoke(jmsg.ToString());
        }
        else
            onFailure?.Invoke(jmsg.ToString());
    }

    public static async Task InitTwitchEventBus()
    {
        await m_interface.m_eventBus.EstablishSocketConnection();
    }

    // Get list of subscribed events
    public static async void GetSubscribedEvents(Action<string> response, Action<string> onFailure)
    {
        using var req = new HttpRequestMessage();
        //req.RequestUri = new Uri(BaseUri + "/users?login="+m_interface.m_userName);
        req.RequestUri = new Uri(BaseUri + "/eventsub/subscriptions");
        req.Method = HttpMethod.Get;
        req.Headers.Add("Client-Id", Context.TwitchID);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Context.TwitchOAuthKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

        using HttpResponseMessage msg = await m_interface.m_client.SendAsync(req);
        if (msg.StatusCode == System.Net.HttpStatusCode.OK)
        {
            Context.ConnectionSuccess = true;
            string json = await msg.Content.ReadAsStringAsync();

            response(json);
        }
        else
        {
            string json = await msg.Content.ReadAsStringAsync();
            if (onFailure != null) onFailure?.Invoke(json);
        }
    }

}