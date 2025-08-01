using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

using VintageCCIClient;
using System.Net.Http;
using Newtonsoft.Json.Linq;

using CCIEvents;
using System.Collections.Generic;
using System;

namespace VintageCCI
{
    public class VintageCCI : ModSystem
    {
        const string VINTAGECCI_FILE_PREFIX = "VintageCCI_event";
        const string MANIFEST_FILENAME = VINTAGECCI_FILE_PREFIX + "_manifest.json";

        private TwitchUsernamePrompt m_prompt;
        private CCIClient m_client;
        //private TwitchInterface m_interface;

        #region [ Twitch Stuff ]
        private bool ToggleTwitchUsernamePrompt(KeyCombination combo)
        {
            if (m_prompt.IsOpened())
                m_prompt.TryClose();
            else
                m_prompt.TryOpen();
            
            return true;
        }

        private void RegisterVariables()
        {
            CCIClient.RegisterVariable<bool>("the_scrongl", false);
        }

        private void InitTwitchPrompt(ICoreClientAPI api)
        {
            api.Input.RegisterHotKey(
                "twitchusername", 
                "Press this to enter your Twitch Username", 
                GlKeys.M, 
                HotkeyType.GUIOrOtherControls
            );
            m_prompt = new TwitchUsernamePrompt(api);
            m_prompt.OnSaveCallback = () => {
                CCIClient.AuthenticateClient(m_prompt.Username, () => m_prompt.OnClose(), null);
            };
            api.Input.SetHotKeyHandler("twitchusername", ToggleTwitchUsernamePrompt);
        }

        // Keeping this here for later
        //private void MakeEvent()
        //{
        //    ev = new Event();
        //    ev.Trigger = new Trigger(TriggerType.CHAT_MESSAGE, "!die");
        //    ev.Conditions.Add(Condition.Create<bool>("the_scrongl", false, CompareType.EQUAL, ConditionJoin.NONE));
        //    ev.Bindings.Add(new Binding(BindingType.COMMAND, "/kill"));
        //    api.StoreModConfig(ev.ToJson(), filename);          
        //}

        private bool TryLoadEvents(ICoreClientAPI api)
        {
            try
            {
                var manifest = api.LoadModConfig(MANIFEST_FILENAME);
                JObject m;
                if (manifest != null)
                {
                    if (manifest.Token.Type != JTokenType.Object)
                    {
                        Mod.Logger.Notification("Manifest is not the proper format");
                        return false;
                    }
                    m = (JObject)manifest.Token;

                    if (!m.ContainsKey("manifest") || m["manifest"].Type != JTokenType.Array)
                    {
                        Mod.Logger.Notification("Manifest is not the proper format");
                        return false;
                    }

                    foreach (var event_name in m["manifest"])
                    {
                        if (event_name.Type != JTokenType.String)
                        {
                            Mod.Logger.Notification("Manifest is not the proper format");
                            return false;
                        }

                        Event e;
                        if (!TryLoadEvent(api, event_name.ToString(), out e))
                        {
                            Mod.Logger.Notification($"Failed to load file \"{event_name}\", continuing.");
                            continue;
                        }

                        if (!CCIClient.RegisterEvent(e))
                        {
                            Mod.Logger.Notification($"Failed to parse event \"{event_name}\", continuing.");
                            continue;
                        }
                    }
                }
                else
                {
                    Mod.Logger.Notification($"Manifest file \"{MANIFEST_FILENAME}\" not found! Making a new one.");
                    m = new JObject(){
                        new JProperty("manifest", new JArray())
                    };
                    api.StoreModConfig(m, MANIFEST_FILENAME);
                }
                return true;
            }
            catch (Exception e)
            {
                Mod.Logger.Notification($"Failed to load due to an exception: {e.ToString()}");
                return false;
            }
        }

        private bool TryLoadEvent(ICoreClientAPI api, string filename, out Event ev)
        {
            try
            {
                var res = api.LoadModConfig(filename);
                if (res == null)
                {
                    ev = null;
                    return false;
                }
                else 
                {
                    JToken result = res.Token;
                    if (!Event.FromJson((JObject)result, out ev))
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Mod.Logger.Notification($"Failed to load due to an exception: {ex.ToString()}");
                ev = null;
                return false;
            }
        }
        #endregion

        // Called on server and client
        public override void Start(ICoreAPI api)
        {
            
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            if (TwitchInterface.Instance != null)
                TwitchInterface.Reset();
            
            m_client = new CCIClient(api);
            InitTwitchPrompt(api);
            RegisterVariables();

            Event ev;
            TryLoadEvents(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Mod.Logger.Notification("Started server side!");
        }
    }
}
