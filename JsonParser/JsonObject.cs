using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonParser
{
    public abstract class JsonObject
    {
        public JsonObject this[int a]
        {
            get
            {
                JsonArray array = this as JsonArray;
                if (array == null)
                    return null;
                    //throw new JsonInvalidAccessException($"This type cannot be indexed by integer! {a}\n{this.ToString()}");
                return array[a];
            }
        }
        public JsonObject this[string a]
        {
            get
            {
                return this[new JsonString(a)];
            }
        }
        public JsonObject this[JsonString a]
        {
            get
            {
                JsonClass @class = this as JsonClass;
                if (@class == null)
                    return null;
                    //throw new JsonInvalidAccessException($"This type cannot be indexed for property! {a.ToString()}\n{this.ToString()}");
                return @class[a];
            }
        }

        public static JsonObject ParseFromStream(Stream stream)
        {
            int c = stream.ReadByte();
            if (c == -1) throw new JsonParseException("Unexpected end of stream!");
            char ch = (char)c;
            stream.Position = stream.Position - 1;
            JsonObject value;
            if (ch == '[')
            {
                value = JsonArray.ParseFromStream(stream);
            }
            else if (ch == '{')
            {
                value = JsonClass.ParseFromStream(stream);
            }
            else if (ch == '"')
            {
                value = JsonString.ParseFromStream(stream);
            }
            else if (char.IsDigit(ch))
            {
                value = JsonDouble.ParseFromStream(stream);
            }
            else
            {
                value = JsonBool.ParseFromStream(stream);
            }
            return value;

        }
    }
}
