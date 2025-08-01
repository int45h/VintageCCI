using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;
using System.Linq;

namespace CCIVariables
{
    public struct Variable
    {
        public string Name;
        public object Value;
        public Type ValueType;

        public Variable(string name, object value, Type type)
        {
            Name = name;
            Value = value;
            ValueType = type;
        }

        public static Variable Create<T>(string name, object value)
        {
            return new Variable(name, value, typeof(T));
        }
    }
}