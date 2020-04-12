using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonParser
{
    public abstract class JsonException : Exception
    {
        public JsonException(string msg) : base(msg)
        {

        }
    }

    public class JsonPropertyNotFoundException : JsonException
    {
        public JsonPropertyNotFoundException(string msg) : base(msg)
        {

        }
    }

    public class JsonInvalidAccessException : JsonException
    {
        public JsonInvalidAccessException(string msg) : base(msg)
        {

        }
    }

    public class JsonParseException : JsonException
    {
        public JsonParseException(string msg) : base(msg)
        {

        }
    }
}
