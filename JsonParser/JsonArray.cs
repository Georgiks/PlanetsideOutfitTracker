using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonParser
{
    public sealed class JsonArray : JsonObject
    {
        private List<JsonObject> InnerList { get; }

        public JsonArray(List<JsonObject> lst)
        {
            InnerList = lst;
        }

        public int Length => InnerList.Count;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < InnerList.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(InnerList[i].ToString());
            }
            sb.Append(']');
            return sb.ToString();
        }

        public new JsonObject this[int i]
        {
            get
            {
                if (i < 0 || i >= InnerList.Count) return null;
                return InnerList[i];
            }
        }

        public static new JsonArray ParseFromStream(Stream stream)
        {
            char ch;
            if ((ch = (char)stream.ReadByte()) != '[') throw new JsonParseException($"Json first character error! {ch}");

            List<JsonObject> lst = new List<JsonObject>();
            if ((char)stream.ReadByte() == ']') return new JsonArray(lst);
            else stream.Position = stream.Position - 1;

            do
            {
                JsonObject value = JsonObject.ParseFromStream(stream);
                lst.Add(value);
            } while ((ch = (char)stream.ReadByte()) == ',');
            if (ch != ']') throw new JsonParseException($"Class end character error! {ch}");
            return new JsonArray(lst);
        }
    }
}
