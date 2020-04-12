using JsonParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    static class WeaponsCache
    {

        static Dictionary<JsonString, JsonString> ItemIdToWeaponName { get; } = new Dictionary<JsonString, JsonString>();
        static WeaponsCache()
        {
            Console.WriteLine("Weapons cache loading...");
            string allWeaponsRequest = @"https://census.daybreakgames.com/s:georgik/get/ps2/item_to_weapon/?c:limit=2000&c:join=item^show:name^inject_at:item";
            JsonObject json = PS2APIUtils.RestAPIRequest(allWeaponsRequest).GetAwaiter().GetResult();
            var weapons = json?["item_to_weapon_list"] as JsonArray;
            if (weapons == null) return;
            for (int i = 0; i < weapons.Length; i++)
            {
                JsonString itemId = weapons[i]?["item_id"] as JsonString;
                JsonString name = weapons[i]?["item"]?["name"]?["en"] as JsonString;
                if (itemId == null || name == null) continue;
                ItemIdToWeaponName.Add(itemId, name);
            }
            Console.WriteLine("Weapons cache loaded!");
        }

        public static JsonString GetName(JsonString id)
        {
            JsonString name;
            if (ItemIdToWeaponName.TryGetValue(id, out name))
            {
                return name;
            }
            return id;
        }
        public static bool TryGetName(JsonString id, out JsonString name)
        {
            return ItemIdToWeaponName.TryGetValue(id, out name);
        }
    }
}
