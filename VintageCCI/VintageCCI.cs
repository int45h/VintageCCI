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

        private bool TryLoadEvent(ICoreClientAPI api, string filename, out Event ev)
        {
            try
            {
                var res = api.LoadModConfig(filename);
                if (res == null)
                {
                    ev = new Event();
                    ev.Trigger = new Trigger(TriggerType.CHAT_MESSAGE, "!die");
                    ev.Conditions.Add(Condition.Create<bool>("the_scrongl", false, CompareType.EQUAL, ConditionJoin.NONE));
                    ev.Bindings.Add(new Binding(BindingType.COMMAND, "/kill"));
                    
                    api.StoreModConfig(ev.ToJson(), filename);
                }
                else 
                {
                    JToken result = res.Token;
                    Event.FromJson((JObject)result, out ev);
                }

                return true;
            }
            catch (Exception ex)
            {
                ev = null;
                return false;
            }
        }
        #endregion

        // Called on server and client
        public override void Start(ICoreAPI api)
        {
            var _event = new Event();
            try
            {
                
                Mod.Logger.Notification("event loaded");
                return;
            }
            catch (System.Exception e)
            {
                Mod.Logger.Notification($"Failed to load event: {e.ToString()}");
            }

            string ev_str = _event.ToString();
            Mod.Logger.Notification(ev_str);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            if (TwitchInterface.Instance != null)
                TwitchInterface.Reset();
            
            m_client = new CCIClient(api);
            InitTwitchPrompt(api);
            RegisterVariables();

            Event ev;
            if (TryLoadEvent(api, "event_scrongl.json", out ev))
                CCIClient.RegisterEvent(ev);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Mod.Logger.Notification("Started server side!");
        }
    }
}
