using JsonParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    /// <summary>
    /// Dynamic cache for player names resolved from game's database with thread safety
    /// </summary>
    static class PlayerCache
    {
        /// For how long are cache items valid
        private static TimeSpan cacheTimeoutMinutes = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Cache item record
        /// </summary>
        struct CacheStruct
        {
            public DateTime cachedTime;
            public NameOutfitFactionRecord cachedValue;
        }

        static ReaderWriterLockSlim rwl = new ReaderWriterLockSlim();

        /// <summary>
        /// Actual cached players
        /// </summary>
        static Dictionary<JsonString, CacheStruct> ResolvedPlayers { get; } = new Dictionary<JsonString, CacheStruct>();
        /// <summary>
        /// Player resolution queries in progress
        /// </summary>
        static Dictionary<JsonString, SemaphoreSlim> QueriesInProgress { get; } = new Dictionary<JsonString, SemaphoreSlim>();

        static PlayerCache()
        {
            Console.WriteLine("Player cache initiated");
        }

        /// <summary>
        /// Gets player name from cache if present or otherwise directly from Rest APIa
        /// </summary>
        public static async Task<NameOutfitFactionRecord> GetPlayer(JsonString id)
        {
            CacheStruct cacheItem;
            if (id == null || id.InnerString == "0") return default(NameOutfitFactionRecord);

            try
            {
                // check whether player is cached and still valid (fast check)
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

                // check if this player's query is already in progress and either get that query's semaphore or create new one
                SemaphoreSlim queryLock;
                lock (QueriesInProgress)
                {
                    if (!QueriesInProgress.TryGetValue(id, out queryLock))
                    {
                        QueriesInProgress.Add(id, queryLock = new SemaphoreSlim(1, 1));
                    }
                }

                // wait for query lock
                await queryLock.WaitAsync();
                try
                {
                    // while we waited for query semaphore, someone else finished query and result is now available
                    if (ResolvedPlayers.TryGetValue(id, out cacheItem) && DateTime.Now - cacheItem.cachedTime < cacheTimeoutMinutes)
                    {
                        return cacheItem.cachedValue;
                    }

                    // fetch player info from Rest API
                    NameOutfitFactionRecord record = await FetchPlayerInfo(id);

                    // if all mandatory record items are valid, save the record to cache
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

        /// <summary>
        /// Get the player info from game's databse
        /// </summary>
        static private async Task<NameOutfitFactionRecord> FetchPlayerInfo(JsonString id)
        {
            JsonObject json;
            int retry = 0;
            // try to resolve player info from database at most 3 times before returning unresolved record
            do
            {
                string uri = $@"http://census.daybreakgames.com/s:{PS2APIConstants.ServiceId}/get/ps2/character_name/?character_id={id.InnerString}&c:join=character^inject_at:character^show:faction_id(outfit_member_extended^inject_at:outfit^show:alias%27name)";
                json = await PS2APIUtils.RestAPIRequestClient(uri);

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
