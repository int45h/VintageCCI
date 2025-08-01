using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

using CCIEvents;
using CCIVariables;

using System.Linq;

namespace VintageCCIClient
{
    // Used for storing your username
    public class TwitchConfigWriter
    {

    }

    // TO-DO: Make this load the twitch key from a file
    public class TwitchUsernamePrompt : GuiDialog
    {
        private ElementBounds m_windowBounds;
        public string Username = "";
        public override string ToggleKeyCombinationCode => "TwitchUsernamePrompt";

        public Action OnSaveCallback;
        public TwitchUsernamePrompt(ICoreClientAPI capi) : base(capi)
        {
            CreateDialogue();
        }

        public void CreateDialogue()
        {
            // Set window bounds to be auto sized
            m_windowBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            
            // Set the text box bounds to be 300x100, with 40 px of vertical padding on top
            ElementBounds msg_bounds = ElementBounds.Fixed(0, 40, 300, 100);
            ElementBounds text_bounds = ElementBounds.Fixed(0, msg_bounds.absFixedY+70, 280, 20);
            ElementBounds btn_bounds = ElementStdBounds.MenuButton(0, EnumDialogArea.CenterBottom);
            
            // Bounds of the background
            ElementBounds bg_bounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bg_bounds.BothSizing = ElementSizing.FitToChildren;
            bg_bounds.WithChildren(msg_bounds, text_bounds, btn_bounds);

            // Create the prompt
            this.SingleComposer = capi.Gui.CreateCompo("TwitchUsernamePrompt", m_windowBounds)
                .AddShadedDialogBG(bg_bounds)
                .AddDialogTitleBar("", OnClose)
                .AddStaticText(
                    "Enter your Twitch username here. Upon hitting save, a browser window will open.", 
                    CairoFont.WhiteDetailText(),
                    msg_bounds
                )
                .AddTextInput(text_bounds, OnTextChanged, CairoFont.WhiteDetailText())
                .AddButton("Save", new ActionConsumable(OnSave), btn_bounds)
                .Compose();
        }

        public void OnTextChanged(string newTxt)
        {
            Username = newTxt;
        }

        public bool OnSave()
        {
            OnSaveCallback?.Invoke();
            return true;
        }

        public void OnClose()
        {
            TryClose();
        }
    }

    // Singleton that represents the state of the game
    public class CCIClient
    {
        private ICoreClientAPI m_clientAPI;
        private static CCIClient m_instance;
        private Dictionary<Trigger, Event> m_events = new Dictionary<Trigger, Event>();
        private List<Variable> m_variables = new List<Variable>();

        public static CCIClient Instance {get => m_instance;}
        public TwitchInterface TwitchInterface;

        public CCIClient(ICoreClientAPI api)
        {
            if (m_instance == null)
                m_instance = this;

            m_clientAPI = api;
            TwitchInterface = new TwitchInterface();
        }

        public void OnConnectionSuccess(string msg){}
        public void OnConnectionError(string msg){}

        public static void AuthenticateClient(string username, Action onSuccess, Action onError)
        {
            TwitchInterface.Reset();
            TwitchInterface.Context.Username = username;

            TwitchInterface.EstablishConnectionAsync((s) => {
                Instance.OnConnectionSuccess(s);
                onSuccess?.Invoke();
            }, (s) => {
                Instance.OnConnectionError(s);
                onError?.Invoke();
            });
                
            TwitchInterface.SubscribeChatEvent(Instance.HandleChatEvents);
        }

        public static bool RegisterVariable<T>(string name, object value) => RegisterVariable(name, value, typeof(T));
        public static bool RegisterVariable(string name, object value, Type t)
        {
            Variable v;
            if (Instance.GetVariable(name, out v))
                return false;
            
            Instance.m_variables.Add(new Variable(name, value, t));
            return true;
        }

        public static bool RegisterEvent(Event _event)
        {
            if (Instance.m_events.ContainsKey(_event.Trigger))
                return false;
            
            Instance.m_events.Add(_event.Trigger, _event);
            return true;
        }

        public bool GetVariable(string name, out Variable v)
        {
            var variable = m_variables.Where(v => string.Compare(v.Name, name) == 0);
            if (variable.Count() < 1)
                goto fail;

            v = variable.First();
            return true;

            fail:
                v = new Variable();
                return false;
        }

        private bool EvaluateCondition(bool lastResult, ConditionJoin lastJoin, Condition c)
        {
            bool currentResult = false;

            Variable v;
            if (!GetVariable(c.Variable, out v))
                return false;
            
            switch (c.Type)
            {
                case CompareType.EQUAL: 
                if (c.ValueType != v.ValueType)
                {
                    currentResult = false;
                    break;
                }

                switch (c.Value)
                {
                    case byte cv:       currentResult = (byte)v.Value == cv;       break;
                    case ushort cv:     currentResult = (ushort)v.Value == cv;     break;
                    case short cv:      currentResult = (short)v.Value == cv;      break;
                    case int cv:        currentResult = (int)v.Value == cv;        break;
                    case uint cv:       currentResult = (uint)v.Value == cv;       break;
                    case long cv:       currentResult = (long)v.Value == cv;       break;
                    case ulong cv:      currentResult = (ulong)v.Value == cv;      break;
                    case float cv:      currentResult = (float)v.Value == cv;      break;
                    case double cv:     currentResult = (double)v.Value == cv;     break;
                    case decimal cv:    currentResult = (decimal)v.Value == cv;    break;
                    case bool cv:       currentResult = (bool)v.Value == cv;       break;
                    case string cv:     currentResult = string.Compare((string)v.Value, cv) == 0; break;
                    case DateTime cv:   currentResult = DateTime.Compare((DateTime)v.Value, cv) == 0; break;
                    default: currentResult = false; break;
                }
                break;
                case CompareType.NOT_EQUAL:
                if (c.ValueType != v.ValueType)
                {
                    currentResult = false;
                    break;
                }

                switch (c.Value)
                {
                    case byte cv:       currentResult = (byte)v.Value != cv;       break;
                    case ushort cv:     currentResult = (ushort)v.Value != cv;     break;
                    case short cv:      currentResult = (short)v.Value != cv;      break;
                    case int cv:        currentResult = (int)v.Value != cv;        break;
                    case uint cv:       currentResult = (uint)v.Value != cv;       break;
                    case long cv:       currentResult = (long)v.Value != cv;       break;
                    case ulong cv:      currentResult = (ulong)v.Value != cv;      break;
                    case float cv:      currentResult = (float)v.Value != cv;      break;
                    case double cv:     currentResult = (double)v.Value != cv;     break;
                    case decimal cv:    currentResult = (decimal)v.Value != cv;    break;
                    case bool cv:       currentResult = (bool)v.Value != cv;       break;
                    case string cv:     currentResult = string.Compare((string)v.Value, cv) != 0; break;
                    case DateTime cv:   currentResult = DateTime.Compare((DateTime)v.Value, cv) != 0; break;
                    default: currentResult = false; break;
                }
                break;
                case CompareType.LESS_THAN:
                if (c.ValueType != v.ValueType)
                {
                    currentResult = false;
                    break;
                }

                switch (c.Value)
                {
                    case byte cv:       currentResult = (byte)v.Value < cv;       break;
                    case ushort cv:     currentResult = (ushort)v.Value < cv;     break;
                    case short cv:      currentResult = (short)v.Value < cv;      break;
                    case int cv:        currentResult = (int)v.Value < cv;        break;
                    case uint cv:       currentResult = (uint)v.Value < cv;       break;
                    case long cv:       currentResult = (long)v.Value < cv;       break;
                    case ulong cv:      currentResult = (ulong)v.Value < cv;      break;
                    case float cv:      currentResult = (float)v.Value < cv;      break;
                    case double cv:     currentResult = (double)v.Value < cv;     break;
                    case decimal cv:    currentResult = (decimal)v.Value < cv;    break;
                    case DateTime cv:   currentResult = DateTime.Compare((DateTime)v.Value, cv) < 0; break;
                    default: currentResult = false; break;
                }
                break;
                case CompareType.GREATER_THAN:
                if (c.ValueType != v.ValueType)
                {
                    currentResult = false;
                    break;
                }

                switch (c.Value)
                {
                    case byte cv:       currentResult = (byte)v.Value > cv;       break;
                    case ushort cv:     currentResult = (ushort)v.Value > cv;     break;
                    case short cv:      currentResult = (short)v.Value > cv;      break;
                    case int cv:        currentResult = (int)v.Value > cv;        break;
                    case uint cv:       currentResult = (uint)v.Value > cv;       break;
                    case long cv:       currentResult = (long)v.Value > cv;       break;
                    case ulong cv:      currentResult = (ulong)v.Value > cv;      break;
                    case float cv:      currentResult = (float)v.Value > cv;      break;
                    case double cv:     currentResult = (double)v.Value > cv;     break;
                    case decimal cv:    currentResult = (decimal)v.Value > cv;    break;
                    case DateTime cv:   currentResult = DateTime.Compare((DateTime)v.Value, cv) > 0; break;
                    default: currentResult = false; break;
                }
                break;
                case CompareType.LESS_THAN_OR_EQUAL:
                if (c.ValueType != v.ValueType)
                {
                    currentResult = false;
                    break;
                }

                switch (c.Value)
                {
                    case byte cv:       currentResult = (byte)v.Value <= cv;       break;
                    case ushort cv:     currentResult = (ushort)v.Value <= cv;     break;
                    case short cv:      currentResult = (short)v.Value <= cv;      break;
                    case int cv:        currentResult = (int)v.Value <= cv;        break;
                    case uint cv:       currentResult = (uint)v.Value <= cv;       break;
                    case long cv:       currentResult = (long)v.Value <= cv;       break;
                    case ulong cv:      currentResult = (ulong)v.Value <= cv;      break;
                    case float cv:      currentResult = (float)v.Value <= cv;      break;
                    case double cv:     currentResult = (double)v.Value <= cv;     break;
                    case decimal cv:    currentResult = (decimal)v.Value <= cv;    break;
                    case DateTime cv:   currentResult = DateTime.Compare((DateTime)v.Value, cv) <= 0; break;
                    default: currentResult = false; break;
                }
                break;
                case CompareType.GREATER_THAN_OR_EQUAL:
                if (c.ValueType != v.ValueType)
                {
                    currentResult = false;
                    break;
                }

                switch (c.Value)
                {
                    case byte cv:       currentResult = (byte)v.Value >= cv;       break;
                    case ushort cv:     currentResult = (ushort)v.Value >= cv;     break;
                    case short cv:      currentResult = (short)v.Value >= cv;      break;
                    case int cv:        currentResult = (int)v.Value >= cv;        break;
                    case uint cv:       currentResult = (uint)v.Value >= cv;       break;
                    case long cv:       currentResult = (long)v.Value >= cv;       break;
                    case ulong cv:      currentResult = (ulong)v.Value >= cv;      break;
                    case float cv:      currentResult = (float)v.Value >= cv;      break;
                    case double cv:     currentResult = (double)v.Value >= cv;     break;
                    case decimal cv:    currentResult = (decimal)v.Value >= cv;    break;
                    case DateTime cv:   currentResult = DateTime.Compare((DateTime)v.Value, cv) >= 0; break;
                    default: currentResult = false; break;
                }
                break;
            }

            // Compare with last result
            switch (lastJoin)
            {
                case ConditionJoin.NONE:    return currentResult;
                case ConditionJoin.AND:     return lastResult & currentResult;
                case ConditionJoin.OR:      return lastResult | currentResult;
                case ConditionJoin.NAND:    return !(lastResult & currentResult);
                case ConditionJoin.NOR:     return !(lastResult | currentResult);
                case ConditionJoin.XOR:     return lastResult ^ currentResult;
                default: return false;
            }
        }

        private void EvaluateBindings(Binding b)
        {
            switch (b.Type)
            {
                case BindingType.COMMAND: 
                    m_clientAPI.SendChatMessage(b.Command); 
                break;
                default: return;
            }
        }

        private void HandleChatEvents(string chatter, string message)
        {
            m_clientAPI.SendChatMessage($"[Twitch] {chatter}: {message}");
            var events = m_events.Where((e) => e.Key.Type == TriggerType.CHAT_MESSAGE && message.Contains(e.Key.Command)).Select(e => e.Value);
            if (events.Count() < 1)
                return;
            
            foreach (var ev in events)
            {
                bool currentResult = false;
                ConditionJoin lastJoin = ConditionJoin.NONE;
                foreach (var c in ev.Conditions)
                {
                    currentResult = EvaluateCondition(currentResult, lastJoin, c);
                    lastJoin = c.Join;
                }

                if (!currentResult)
                    continue;

                foreach (var b in ev.Bindings)
                    EvaluateBindings(b);
            }
        }
    }
}