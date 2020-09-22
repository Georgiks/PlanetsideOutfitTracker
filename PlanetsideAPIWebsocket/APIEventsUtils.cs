using JsonParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    /// <summary>
    /// Various utility methods for working with PS2 websocket API
    /// </summary>
    public static class PS2APIEventUtils
    {
        private static void FillAdditionalInfo(Dictionary<JsonString, JsonObject> dict, JsonArray characters, JsonArray events, JsonArray worlds)
        {
            if (characters != null) dict.Add(new JsonString("characters"), characters);
            if (events != null) dict.Add(new JsonString("eventNames"), events);
            if (worlds != null) dict.Add(new JsonString("worlds"), worlds);
        }

        /// <summary>
        /// Creates JsonObject for subscribing to given events for given players and/or worlds
        /// </summary>
        public static JsonClass GetSubscribeEvent(JsonArray characters = null, JsonArray events = null, JsonArray worlds = null)
        {
            var dict = new Dictionary<JsonString, JsonObject>();
            dict.Add(new JsonString("service"), new JsonString("event"));
            dict.Add(new JsonString("action"), new JsonString("subscribe"));
            FillAdditionalInfo(dict, characters, events, worlds);
            return new JsonClass(dict);
        }

        /// <summary>
        /// Creates JsonObject for unsubscribing from given events
        /// </summary>
        public static JsonClass GetUnsubscribeEvent(JsonArray characters = null, JsonArray events = null, JsonArray worlds = null)
        {
            var dict = new Dictionary<JsonString, JsonObject>();
            dict.Add(new JsonString("service"), new JsonString("event"));
            dict.Add(new JsonString("action"), new JsonString("clearSubscribe"));
            FillAdditionalInfo(dict, characters, events, worlds);
            return new JsonClass(dict);
        }

        /// <summary>
        /// Creates JsonObject for unsubscribing from all events
        /// </summary>
        public static JsonClass GetUnsubscribeAllEvent(JsonArray characters = null, JsonArray events = null, JsonArray worlds = null)
        {
            var dict = new Dictionary<JsonString, JsonObject>();
            dict.Add(new JsonString("service"), new JsonString("event"));
            dict.Add(new JsonString("action"), new JsonString("clearSubscribe"));
            dict.Add(new JsonString("all"), new JsonString("true"));
            return new JsonClass(dict);
        }
    }
}
