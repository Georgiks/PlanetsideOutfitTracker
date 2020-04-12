using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonParser
{
    public sealed class JsonClass : JsonObject
    {
        private Dictionary<JsonString, JsonObject> InnerDictionary { get; }
        
        public JsonClass(Dictionary<JsonString, JsonObject> dict)
        {
            InnerDictionary = dict;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('{');
            var keys = InnerDictionary.Keys.ToList();
            for (int i = 0; i < InnerDictionary.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(keys[i].ToString()).Append(':').Append(InnerDictionary[keys[i]].ToString());
            }
            sb.Append('}');
            return sb.ToString();
        }

        public new JsonObject this[JsonString key]
        {
            get
            {
                JsonObject val;
                if (InnerDictionary.TryGetValue(key, out val)) return val;
                return null;
                //throw new JsonPropertyNotFoundException($"Json class does not have specified property {key}\n{this.ToString()}");
            }
        }

        public static new JsonClass ParseFromStream(Stream stream)
        {
            char ch;
            if ((ch = (char)stream.ReadByte()) != '{') throw new JsonParseException($"Json first character error! {ch}");
            Dictionary<JsonString, JsonObject> dict = new Dictionary<JsonString, JsonObject>();
            if ((char)stream.ReadByte() == '}') return new JsonClass(dict);
            else stream.Position = stream.Position - 1;
            do
            {
                JsonString key = JsonString.ParseFromStream(stream);
                if ((ch = (char)stream.ReadByte()) != ':') throw new JsonParseException($"Json property value character error! {ch}");
                JsonObject value = JsonObject.ParseFromStream(stream);
                dict.Add(key, value);
            } while ((ch = (char)stream.ReadByte()) == ',');
            if (ch != '}') throw new JsonParseException($"Class end character error! {ch}");
            return new JsonClass(dict);

        }
    }
}
