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
        static SemaphoreSlim semaphore = new SemaphoreSlim(1);
        static Dictionary<JsonString, CacheStruct> ResolvedPlayers { get; } = new Dictionary<JsonString, CacheStruct>();
        static PlayerCache()
        {
            Console.WriteLine("Player cache initiated");
        }

        public static async Task<NameOutfitFactionRecord> GetPlayer(JsonString id)
        {
            CacheStruct cacheItem;
            if (id == null || id.InnerString == "0") return default(NameOutfitFactionRecord);


            rwl.EnterUpgradeableReadLock();
            try
            {
                if (ResolvedPlayers.TryGetValue(id, out cacheItem))
                {
                    if (DateTime.Now - cacheItem.cachedTime > cacheTimeoutMinutes)
                    {
                        rwl.EnterWriteLock();
                        ResolvedPlayers.Remove(id);
                        rwl.ExitWriteLock();
                    }
                    else return cacheItem.cachedValue;
                }
            } finally
            {
                rwl.ExitUpgradeableReadLock();
            }

            // shit, more threads can access cache at the same time, and since we want to add to a cache, other thread may want do the same -> one fails when adding to cache dictionary!
            // we must ensure only one thread will send request and fill cache
            await semaphore.WaitAsync();
            try
            {
                rwl.EnterReadLock();
                try
                {
                    if (ResolvedPlayers.TryGetValue(id, out cacheItem))
                    {
                        return cacheItem.cachedValue;
                    }
                } finally
                {
                    rwl.ExitReadLock();
                }

                var json = await PS2APIUtils.RestAPIRequest($@"http://census.daybreakgames.com/s:georgik/get/ps2/character_name/?character_id={id.InnerString}&c:join=character^inject_at:character^show:faction_id(outfit_member_extended^inject_at:outfit^show:alias%27name)");
                NameOutfitFactionRecord record = new NameOutfitFactionRecord();
                bool invalid = false;
                if ((record.Name = (json?["character_name_list"]?[0]?["name"]?["first"] as JsonString)?.InnerString) == null)
                {
                    record.Name = $"<Character:{id.InnerString}>";
                    invalid = true;
                }
                if ((record.Faction = (json?["character_name_list"]?[0]?["character"]?["faction_id"] as JsonString)?.InnerString) == null)
                {
                    record.Faction = $"<CharacterFaction:{id.InnerString}>";
                    invalid = true;
                }
                // no outfit is possible value
                record.Outfit = (json?["character_name_list"]?[0]?["character"]?["outfit"]?["alias"] as JsonString)?.InnerString;
                record.Id = id;

                if (!invalid)
                {
                    cacheItem = new CacheStruct();
                    cacheItem.cachedTime = DateTime.Now;
                    cacheItem.cachedValue = record;

                    rwl.EnterWriteLock();
                    try
                    {
                        ResolvedPlayers.Add(id, cacheItem);
                        if (ResolvedPlayers.Count % 25 == 0) Console.WriteLine("Cached players: " + ResolvedPlayers.Count);
                    } finally
                    {
                        rwl.ExitWriteLock();
                    }
                }
                return cacheItem.cachedValue;
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
