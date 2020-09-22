using JsonParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    /// <summary>
    /// One-time cache for loadout names
    /// </summary>
    static class LoadoutCache
    {

        static Dictionary<JsonString, JsonString> LoadoutIdToName { get; } = new Dictionary<JsonString, JsonString>();

        static LoadoutCache()
        {
            Console.WriteLine("Loadout cache loading...");
            // get all loadout names and save it to dictionary
            string allLoadoutsRequest = $@"http://census.daybreakgames.com/s:{PS2APIConstants.ServiceId}/get/ps2/loadout/?c:limit=100&c:show=loadout_id,profile_id&c:join=profile^on:profile_id^show:name^inject_at:profile";
            JsonObject json = PS2APIUtils.RestAPIRequest(allLoadoutsRequest).GetAwaiter().GetResult();
            var loadouts = json?["loadout_list"] as JsonArray;
            if (loadouts == null) return;
            for (int i = 0; i < loadouts.Length; i++)
            {
                JsonString loadoutId = loadouts[i]?["loadout_id"] as JsonString;
                JsonString name = loadouts[i]?["profile"]?["name"]?["en"] as JsonString;
                if (loadoutId == null || name == null) continue;
                LoadoutIdToName.Add(loadoutId, name);
            }
            Console.WriteLine("Loadout cache loaded!");

        }

        public static JsonString GetName(JsonString id)
        {
            JsonString name;
            if (LoadoutIdToName.TryGetValue(id, out name))
            {
                return name;
            }
            return id;
        }
        public static bool TryGetName(JsonString id, out JsonString name)
        {
            return LoadoutIdToName.TryGetValue(id, out name);
        }
    }
}
