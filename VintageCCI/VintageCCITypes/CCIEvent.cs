using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;
using System.Linq;

namespace CCIEvents
{
    public enum TriggerType
    {
        INVALID,
        CHAT_MESSAGE
    }

    public struct Trigger
    {
        public TriggerType Type;
        public string Command; // TO-DO: replace this with a response type

        public Trigger(TriggerType type, string command)
        {
            Type = type;
            Command = command;
        }

        public static TriggerType StringToTriggerType(string type)
        {
            switch (type)
            {
                case "chat_message": return TriggerType.CHAT_MESSAGE;
                default: return TriggerType.INVALID;
            }
        }

        public static string TriggerTypeToString(TriggerType type)
        {
            switch(type)
            {
                case TriggerType.CHAT_MESSAGE: return "chat_message";
                default: return "???";
            }
        }

        public static bool FromJson(JObject json, out Trigger trigger)
        {
            if (json.Type != JTokenType.Object)
                goto fail;

            var keys = json.Properties().Select(p => p.Name);
            if (keys.Count() < 1)
                goto fail;
            
            string key = keys.First();

            TriggerType type = StringToTriggerType(keys.First());
            if (type == TriggerType.INVALID)
                goto fail;

            trigger = new Trigger(type, json[key].ToString());
            return true;

            fail:
                trigger = new Trigger(TriggerType.INVALID, "");
                return false;
        }

        public JObject ToJson()
        {
            return new JObject() { new JProperty(TriggerTypeToString(Type), Command) };
        }
        public override string ToString() => ToJson().ToString(Newtonsoft.Json.Formatting.None);
    }

    public enum ConditionJoin
    {
        INVALID,
        NONE,
        AND,
        OR,
        NAND,
        NOR,
        XOR
    }

    public enum CompareType
    {
        INVALID,
        EQUAL,
        NOT_EQUAL,
        LESS_THAN,
        GREATER_THAN,
        LESS_THAN_OR_EQUAL,
        GREATER_THAN_OR_EQUAL,
    }

    public struct Condition
    {
        public string Variable;
        public object Value;
        public Type ValueType;

        public CompareType Type = CompareType.EQUAL;
        public ConditionJoin Join = ConditionJoin.NONE;

        public Condition(string variable, object value, Type objectType, CompareType type, ConditionJoin join)
        {
            Variable = variable;
            Value = value;
            ValueType = objectType;
            Type = type;
            Join = join;
        }

        public static Condition Create<T>(string variable, object value, CompareType type, ConditionJoin join)
        {
            return new Condition(
                variable,
                value,
                typeof(T),
                type,
                join
            );
        }

        public static CompareType StringToCompareType(string type)
        {
            switch (type)
            {
                case "equal": return CompareType.EQUAL;
                case "not_equal": return CompareType.NOT_EQUAL;
                case "less_than": return CompareType.LESS_THAN;
                case "greater_than": return CompareType.GREATER_THAN;
                case "less_than_or_equal": return CompareType.LESS_THAN_OR_EQUAL;
                case "greater_than_or_equal": return CompareType.GREATER_THAN_OR_EQUAL;
                default: return CompareType.INVALID;
            }
        }

        public static ConditionJoin StringToConditionJoin(string join)
        {
            switch(join)
            {
                case "none": return ConditionJoin.NONE;
                case "and": return ConditionJoin.AND;
                case "or": return ConditionJoin.OR;
                case "nand": return ConditionJoin.NAND;
                case "nor": return ConditionJoin.NOR;
                case "xor": return ConditionJoin.XOR;
                default: return ConditionJoin.INVALID;
            }
        }

        public static string CompareTypeToString(CompareType type)
        {
            switch (type)
            {
                case CompareType.EQUAL: return "equal";
                case CompareType.NOT_EQUAL: return "not_equal";
                case CompareType.LESS_THAN: return "less_than";
                case CompareType.GREATER_THAN: return "greater_than";
                case CompareType.LESS_THAN_OR_EQUAL: return "less_than_or_equal";
                case CompareType.GREATER_THAN_OR_EQUAL: return "greater_than_or_equal";
                default: return "???";
            }
        }

        public static string ConditionJoinToString(ConditionJoin join)
        {
            switch(join)
            {
                case ConditionJoin.NONE: return "none";
                case ConditionJoin.AND: return "and";
                case ConditionJoin.OR: return "or";
                case ConditionJoin.NAND: return "nand";
                case ConditionJoin.NOR: return "nor";
                case ConditionJoin.XOR: return "xor";
                default: return "???";
            }
        }

        public static bool FromJson(JObject json, out Condition condition)
        {
            if (json.Type != JTokenType.Object)
                goto fail;

            if (json.Properties().Count() < 3)
                goto fail;

            condition = new Condition();
            
            // Parse compare type
            if (!json.ContainsKey("compare_type"))
                goto fail;

            var ct = json["compare_type"];
            if (ct.Type != JTokenType.String)
                goto fail;
            
            var compare_type = StringToCompareType(ct.ToString());
            if (compare_type == CompareType.INVALID)
                goto fail;

            condition.Type = compare_type;
            json.Remove("compare_type");

            // Parse condition join
            if (!json.ContainsKey("condition_join"))
                goto fail;

            var cj = json["condition_join"];
            if (cj.Type != JTokenType.String)
                goto fail;

            var condition_join = StringToConditionJoin(cj.ToString());
            if (condition_join == ConditionJoin.INVALID)
                goto fail;

            condition.Join = condition_join;
            json.Remove("condition_join");

            // Parse variable
            string key = json.Properties().Select(p => p.Name).First();
            var v = json[key];

            // Get its type
            switch (v.Type)
            {
                case JTokenType.Integer:    condition.ValueType = typeof(int); break;
                case JTokenType.Float:      condition.ValueType = typeof(float); break;
                case JTokenType.Boolean:    condition.ValueType = typeof(bool); break;
                case JTokenType.String:     condition.ValueType = typeof(string); break;
                case JTokenType.Date:       condition.ValueType = typeof(DateTime); break;
                case JTokenType.Guid:       condition.ValueType = typeof(Guid); break;
                default: goto fail;
            }

            condition.Value = v.ToObject(condition.ValueType);
            condition.Variable = key;
            return true;

            fail:
            condition = new Condition("", false, typeof(bool), CompareType.INVALID, ConditionJoin.INVALID);
            return false;
        }

        public JObject ToJson()
        {
            return new JObject(){
                new JProperty(Variable, Value),
                new JProperty("compare_type", CompareTypeToString(Type)),
                new JProperty("condition_join", ConditionJoinToString(Join))
            };
        }
        public override string ToString() => ToJson().ToString(Newtonsoft.Json.Formatting.None);
    }

    public enum BindingType
    {
        INVALID,
        COMMAND
    }

    public struct Binding
    {
        public BindingType Type;
        public string Command; // TO-DO: replace this with an action type

        public Binding(BindingType type, string command)
        {
            Type = type;
            Command = command;
        }

        public static BindingType StringToBindingType(string type)
        {
            switch (type)
            {
                case "command": return BindingType.COMMAND;
                default: return BindingType.INVALID;
            }
        }

        public static string BindingTypeToString(BindingType type)
        {
            switch (type)
            {
                case BindingType.COMMAND: return "command";
                default: return "???";
            }
        }

        public static bool FromJson(JObject json, out Binding binding)
        {
            if (json.Type != JTokenType.Object)
                goto fail;

            var keys = json.Properties().Select(p => p.Name);
            if (keys.Count() < 1)
                goto fail;
            
            string key = keys.First();

            BindingType type = StringToBindingType(keys.First());
            if (type == BindingType.INVALID)
                goto fail;

            binding = new Binding(type, json[key].ToString());
            return true;

            fail:
                binding = new Binding(BindingType.INVALID, "");
                return false;
        }

        public JObject ToJson()
        {
            JObject obj = new JObject(){ new JProperty(BindingTypeToString(Type), Command) };
            return obj;
        }
        public override string ToString() => ToJson().ToString(Newtonsoft.Json.Formatting.None);
    }

    public class Event
    {
        public Trigger Trigger;
        public List<Condition> Conditions;
        public List<Binding> Bindings;

        public Event()
        {
            Conditions = new List<Condition>();
            Bindings = new List<Binding>();
        }

        public static bool FromJson(JObject json, out Event _event)
        {
            if (json.Type != JTokenType.Object)
                goto fail;

            if (!json.ContainsKey("event"))
                goto fail;
            
            var ev = json["event"];
            if (ev.Type != JTokenType.Object)
                goto fail;
            
            // Check if the event contains the required keys
            var ev_obj = (JObject)ev;
            if (!(ev_obj.ContainsKey("trigger") || 
                ev_obj.ContainsKey("conditions") || 
                ev_obj.ContainsKey("bindings")))
                goto fail;

            _event = new Event();
            
            // Check types of properties in the event
            var t = ev_obj["trigger"];
            if (t.Type != JTokenType.Object)
                goto fail;
            
            var c = ev_obj["conditions"];
            if (c.Type != JTokenType.Array)
                goto fail;

            var b = ev_obj["bindings"];
            if (b.Type != JTokenType.Array)
                goto fail;

            // Parse trigger
            Trigger trigger;
            if (!Trigger.FromJson((JObject)t, out trigger))
                goto fail;
                
            _event.Trigger = trigger;

            // Parse conditions
            JArray carr = (JArray)c;
            foreach (var cd in carr)
            {
                if (cd.Type != JTokenType.Object)
                    goto fail;
                
                Condition condition;
                if (!Condition.FromJson((JObject)cd, out condition))
                    goto fail;
                
                _event.Conditions.Add(condition);
            }

            // Parse bindings
            JArray barr = (JArray)b;
            foreach (var bd in barr)
            {
                if (bd.Type != JTokenType.Object)
                    goto fail;

                Binding binding;
                if (!Binding.FromJson((JObject)bd, out binding))
                    goto fail;

                _event.Bindings.Add(binding);
            }
            
            return true;

            fail:
                _event = null;
                return false;
        }

        public JObject ToJson()
        {
            JObject obj = new JObject(){new JProperty("event", new JObject(){
                new JProperty("trigger", Trigger.ToJson()),
                new JProperty("conditions", new JArray()),
                new JProperty("bindings", new JArray()),
            })};
            foreach (var c in Conditions)
                ((JArray)obj["event"]["conditions"]).Add(c.ToJson());
            foreach (var b in Bindings)
                ((JArray)obj["event"]["bindings"]).Add(b.ToJson());
            
            return obj;
        }
        public override string ToString()
        {
            string output = ToJson().ToString(Newtonsoft.Json.Formatting.None);
            return output;
        }
    }    
}