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
        private static TimeSpan cacheTimeoutMinutes = TimeSpan.FromMinutes(10);
        struct CacheStruct
        {
            public DateTime cachedTime;
            public NameOutfitFactionRecord cachedValue;
        }

        static ReaderWriterLockSlim rwl = new ReaderWriterLockSlim();

        static Dictionary<JsonString, CacheStruct> ResolvedPlayers { get; } = new Dictionary<JsonString, CacheStruct>();
        static Dictionary<JsonString, SemaphoreSlim> QueriesInProgress { get; } = new Dictionary<JsonString, SemaphoreSlim>();
        static PlayerCache()
        {
            Console.WriteLine("Player cache initiated");
        }

        public static async Task<NameOutfitFactionRecord> GetPlayer(JsonString id)
        {
            CacheStruct cacheItem;
            if (id == null || id.InnerString == "0") return default(NameOutfitFactionRecord);

            try
            {
                rwl.EnterReadLock();
                try
                {
                    if (ResolvedPlayers.TryGetValue(id, out cacheItem) && DateTime.Now - cacheItem.cachedTime < cacheTimeoutMinutes)
                    {
                        return cacheItem.cachedValue;
                    }
                }
                finally
                {
                    rwl.ExitReadLock();
                }


                SemaphoreSlim queryLock;
                lock (QueriesInProgress)
                {
                    if (!QueriesInProgress.TryGetValue(id, out queryLock))
                    {
                        QueriesInProgress.Add(id, queryLock = new SemaphoreSlim(1, 1));
                    }
                }

                await queryLock.WaitAsync();
                try
                {
                    if (ResolvedPlayers.TryGetValue(id, out cacheItem) && DateTime.Now - cacheItem.cachedTime < cacheTimeoutMinutes)
                    {
                        return cacheItem.cachedValue;
                    }

                    NameOutfitFactionRecord record = await FetchPlayerInfo(id);

                    if (!record.Name.StartsWith("<") && !record.Faction.StartsWith("<"))
                    {
                        cacheItem.cachedTime = DateTime.Now;
                        cacheItem.cachedValue = record;
                        rwl.EnterWriteLock();
                        try
                        {
                            ResolvedPlayers[id] = cacheItem;
                        }
                        finally
                        {
                            rwl.ExitWriteLock();
                        }
                    }
                    return record;
                }
                finally
                {
                    queryLock.Release();
                }
            } catch (Exception e)
            {
                Console.WriteLine($"Player cache ID:{id}\n{e.ToString()}");
                return default(NameOutfitFactionRecord);
            }
        }
        static private async Task<NameOutfitFactionRecord> FetchPlayerInfo(JsonString id)
        {
            JsonObject json;
            int retry = 0;
            do
            {
                string uri = $@"http://census.daybreakgames.com/s:georgik/get/ps2/character_name/?character_id={id.InnerString}&c:join=character^inject_at:character^show:faction_id(outfit_member_extended^inject_at:outfit^show:alias%27name)";
                json = await PS2APIUtils.RestAPIRequest(uri, timeoutMs: 5000);

            } while (json == null && ++retry < 3);
            NameOutfitFactionRecord record = new NameOutfitFactionRecord();
            if ((record.Name = (json?["character_name_list"]?[0]?["name"]?["first"] as JsonString)?.InnerString) == null)
            {
                record.Name = $"<Character:{id.InnerString}>";
            }
            if ((record.Faction = (json?["character_name_list"]?[0]?["character"]?["faction_id"] as JsonString)?.InnerString) == null)
            {
                record.Faction = $"<CharacterFaction:{id.InnerString}>";
            }
            // no outfit is possible value
            record.Outfit = (json?["character_name_list"]?[0]?["character"]?["outfit"]?["alias"] as JsonString)?.InnerString;
            record.Id = id;

            return record;
        }
    }
}
