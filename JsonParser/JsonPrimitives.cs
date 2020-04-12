using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonParser
{
    public sealed class JsonString : JsonObject
    {
        public string InnerString { get; }

        public static JsonString Empty = new JsonString("");
        public JsonString(string s)
        {
            InnerString = s;
        }

        public override string ToString()
        {
            return "\"" + InnerString.ToString() + "\"";
        }
        
        public static new JsonString ParseFromStream(Stream stream)
        {
            if (stream.ReadByte() != '"') throw new JsonParseException($"Json first character error!");
            StringBuilder sb = new StringBuilder();
            int c;
            bool wasBackslash = false;
            while ((c = stream.ReadByte()) != -1)
            {
                if ((char)c == '"' && !wasBackslash) break;
                if ((char)c == '\\') wasBackslash = true;
                else wasBackslash = false;
                sb.Append((char)c);
            }
            if (c == -1) throw new JsonParseException("Did not find end of string!");
            return new JsonString(sb.ToString());
        }

        public override bool Equals(object obj)
        {
            JsonString other = obj as JsonString;
            if (other == null) return false;
            return InnerString.Equals(other.InnerString);
        }
        public override int GetHashCode()
        {
            return InnerString.GetHashCode();
        }

        public static bool operator ==(JsonString other, JsonString other2)
        {
            return EqualityComparer<JsonString>.Default.Equals(other, other2);
        }
        public static bool operator !=(JsonString other, JsonString other2)
        {
            return !(other == other2);
        }
    }

    public sealed class JsonLong : JsonObject
    {
        public long InnerLong { get; }

        public JsonLong(long l)
        {
            InnerLong = l;
        }

        public override string ToString()
        {
            return InnerLong.ToString();
        }
    }
    public sealed class JsonBool : JsonObject
    {
        public bool InnerBool { get; }

        public JsonBool(bool b)
        {
            InnerBool = b;
        }

        public override string ToString()
        {
            return InnerBool.ToString();
        }

        public static new JsonObject ParseFromStream(Stream stream)
        {
            int c;
            StringBuilder sb = new StringBuilder();
            while ((c = stream.ReadByte()) != -1)
            {
                if (!char.IsLetter((char)c))
                {
                    stream.Position = stream.Position - 1;
                    break;
                }
                sb.Append((char)c);
            }

            string final = sb.ToString();
            if (final == "true") return new JsonBool(true);
            else if (final == "false") return new JsonBool(false);
            else throw new JsonParseException($"Boolean parse failed! '{final}'");
        }
    }

    public sealed class JsonDouble : JsonObject
    {
        public double InnerDouble { get; }

        public JsonDouble(double d)
        {
            InnerDouble = d;
        }

        public override string ToString()
        {
            return InnerDouble.ToString("F");
        }

        public static new JsonObject ParseFromStream(Stream stream)
        {
            bool decimalSeparator = false;
            bool endReached = false;
            StringBuilder sb = new StringBuilder();
            int c = stream.ReadByte();
            while (true)
            {
                if (c == -1) endReached = true;
                char ch = (char)c;
                if (!((ch <= '9' && ch >= '0') || ch == '.')) break;
                if (ch == '.') decimalSeparator = true;
                sb.Append(ch);
                c = stream.ReadByte();
            }
            if (!endReached) stream.Position = stream.Position - 1;
            if (decimalSeparator)
            {
                double d;
                if (!double.TryParse(sb.ToString(), out d)) {
                    throw new JsonParseException($"Number in wrong format! '{sb.ToString()}'");
                }
                return new JsonDouble(d);
            } else
            {
                long l;
                if (!long.TryParse(sb.ToString(), out l)) {
                    throw new JsonParseException($"Number in wrong format! '{sb.ToString()}'");
                }
                return new JsonLong(l);
            }
        }
    }
}
