using JsonParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    static class PlayerCache
    {
        static SemaphoreSlim semaphore = new SemaphoreSlim(1);
        static Dictionary<JsonString, NameOutfitFactionRecord> ResolvedPlayers { get; } = new Dictionary<JsonString, NameOutfitFactionRecord>();
        static PlayerCache()
        {
            Console.WriteLine("Player cache initiated");
        }

        public static async Task<NameOutfitFactionRecord> GetPlayer(JsonString id)
        {
            NameOutfitFactionRecord record;
            if (id == null || id.InnerString == "0") return default(NameOutfitFactionRecord);

            if (ResolvedPlayers.TryGetValue(id, out record))
            {
                return record;
            }
            // shit, more threads can access cache at the same time, and since we want to add to a cache, other thread may want do the same -> one fails when adding to cache dictionary!
            // we must ensure only one thread will send request and fill cache
            await semaphore.WaitAsync();
            try
            {
                if (ResolvedPlayers.TryGetValue(id, out record))
                {
                    return record;
                }

                var json = await PS2APIUtils.RestAPIRequest($@"http://census.daybreakgames.com/s:georgik/get/ps2/character_name/?character_id={id.InnerString}&c:join=character^inject_at:character^show:faction_id(outfit_member_extended^inject_at:outfit^show:alias%27name)");
                record = new NameOutfitFactionRecord();
                if ((record.Name = (json?["character_name_list"]?[0]?["name"]?["first"] as JsonString)?.InnerString) == null) record.Name = $"<Character:{id.InnerString}>";
                if ((record.Faction = (json?["character_name_list"]?[0]?["character"]?["faction_id"] as JsonString)?.InnerString) == null) record.Faction = $"<CharacterFaction:{id.InnerString}>";
                // no outfit is possible value
                record.Outfit = (json?["character_name_list"]?[0]?["character"]?["outfit"]?["alias"] as JsonString)?.InnerString;
                record.Id = id;

                ResolvedPlayers.Add(id, record);
            } finally
            {
                semaphore.Release();
            }
            if (ResolvedPlayers.Count % 25 == 0) Console.WriteLine("Cached players: " + ResolvedPlayers.Count);
            return record;
        }
    }
}
