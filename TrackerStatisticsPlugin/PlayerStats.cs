using JsonParser;
using PlanetsideAPIWebsocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TrackerStatisticsPlugin
{
    /// <summary>
    /// Class representing player's traced information
    /// </summary>
    public class PlayerStats
    {
        /// <summary>
        /// Struct of all tracked information
        /// </summary>
        public struct Statistics
        {
            public int Kills { get; set; }
            public int Deaths { get; set; }
            public int Assists { get; set; }
            public int ConventionalKills { get; set; }
            public int TeamKills { get; set; }
            public int OutfitKills { get; set; }
            public int TeamDeaths { get; set; }
            public int OutfitDeaths { get; set; }
            public int Revives { get; set; }
            public int SquadRevives { get; set; }
            public int OutfitRevives { get; set; }
            public int Suicides { get; set; }
            public int Revived { get; set; }
            public int VehiclesDestroyed { get; set; }
            public int VehiclesLost { get; set; }
            public int VehiclesSelfDestroyed { get; set; }
            public int TeamVehiclesDestroyed { get; set; }
            public int OutfitVehiclesDestroyed { get; set; }
            public int HeadshotEnemyKills { get; set; }
            public int MAXRepairs { get; set; }
            public int Heals { get; set; }
            public int Resupplies { get; set; }
        }

        Statistics _stat = new Statistics();
        public Statistics Stats => _stat;

        List<PlayerLoggingEventRecord> loggingRecords = new List<PlayerLoggingEventRecord>();
        /// <summary>
        /// List of all events produced by this player
        /// </summary>
        public SortedSet<EventRecord> Events { get; } = new SortedSet<EventRecord>(Comparer<EventRecord>.Create((a, b) => Comparer<long>.Default.Compare(a.timestamp, b.timestamp)));
        public JsonString CharacterId { get; }
        public JsonString CharacterName { get; }
        public JsonString OutfitRank { get; }

        DateTime trackingStart = DateTime.Now;

        /// <summary>
        /// Get online time till this moment
        /// </summary>
        public TimeSpan OnlineTime
        {
            get
            {
                return OnlineTimeUntil(DateTime.Now);
            }
        }

        /// <summary>
        /// Estimate this player's online time until specified argument.
        /// </summary>
        /// <param name="dateTime">Estimate online time before this DateTime instance</param>
        public TimeSpan OnlineTimeUntil(DateTime dateTime)
        {
            loggingRecords.Sort((a, b) => Comparer<long>.Default.Compare(a.timestamp, b.timestamp));

            DateTime from = trackingStart;
            TimeSpan onlineTime = new TimeSpan(0);
            for (int i = 0; i < loggingRecords.Count; i++)
            {
                DateTime recordTime = DateTimeOffset.FromUnixTimeSeconds(loggingRecords[i].timestamp).UtcDateTime.ToLocalTime();

                if (recordTime > dateTime)
                    break;
                if (loggingRecords[i] is PlayerLoginEventRecord)
                {
                    from = recordTime;
                }
                else
                {
                    // error-proof, checking when more Logout events happened 
                    if (from.Ticks > 0)
                    {
                        onlineTime += recordTime - from;
                        from = new DateTime(0);
                    }
                }
            }
            // player has not logout yet (and there is evidence that he was onlince in case there is no login record),
            // add his time since login (or beginning of tracking)
            if (from.Ticks > 0 && !(from == trackingStart && Events.Count == 0))
                onlineTime += dateTime - from;
            return onlineTime;
        }

        public PlayerStats(JsonString id, JsonString name, JsonString rank)
        {
            CharacterId = id;
            CharacterName = name;
            OutfitRank = rank;
        }

        public void SetOnline(PlayerLoginEventRecord record)
        {
            loggingRecords.Add(record);

            Events.Add(record);
        }
        public void SetOffline(PlayerLogoutEventRecord record)
        {
            loggingRecords.Add(record);

            Events.Add(record);
        }

        public void RegisterDeathEvent(KillEventRecord record)
        {
            if (record.attacker.Id == record.victim.Id)
            {
                _stat.Suicides++;
            }
            else if (record.attacker.Id == CharacterId)
            {
                if (record.victim.Faction != record.attacker.Faction)
                {
                    _stat.Kills++;
                }
                else
                {
                    _stat.TeamKills++;
                }

                if (!string.IsNullOrEmpty(record.victim.Outfit) && record.victim.Outfit == record.attacker.Outfit)
                    _stat.OutfitKills++;
                if (record.headshot && record.victim.Faction != record.attacker.Faction)
                    _stat.HeadshotEnemyKills++;
                if (record.victim.Faction != record.attacker.Faction && record.weaponName != null && record.weaponName.IndexOf("Orbital Strike", StringComparison.OrdinalIgnoreCase) == -1 && record.attackerVehicle.Name == null)
                    _stat.ConventionalKills++;
            }
            else
            {
                if (record.attacker.Faction != record.victim.Faction)
                    _stat.Deaths++;
                else
                    _stat.TeamDeaths++;
                if (!string.IsNullOrEmpty(record.victim.Outfit) && record.attacker.Outfit == record.victim.Outfit)
                    _stat.OutfitDeaths++;
            }
            Events.Add(record);
        }
        public void RegisterVehicleDestroyed(VehicleDestroyedEventRecord record)
        {
            if (record.destroyedVehicle.Type?.InnerString != PS2APIConstants.TurretVehicleTypeId)
            {
                if (record.attacker.Id == record.victim.Id)
                {
                    _stat.VehiclesSelfDestroyed++;
                }
                else if (record.attacker.Id == CharacterId)
                {
                    if (record.victim.Faction != record.attacker.Faction)
                        _stat.VehiclesDestroyed++;
                    else
                        _stat.TeamVehiclesDestroyed++;
                    if (!string.IsNullOrEmpty(record.victim.Outfit) && record.victim.Outfit == record.attacker.Outfit)
                        _stat.OutfitVehiclesDestroyed++;
                }
                if (record.victim.Id == CharacterId)
                {
                    _stat.VehiclesLost++;
                }
            }

            Events.Add(record);
        }

        public void RegisterRevive(ReviveEventRecord record)
        {
            if (record.reviver.Id == CharacterId)
            {
                if (record.squad)
                    _stat.SquadRevives++;
                else
                    _stat.Revives++;
                if (!string.IsNullOrEmpty(record.revived.Outfit) && record.revived.Outfit == record.reviver.Outfit)
                    _stat.OutfitRevives++;
            }
            else
            {
                _stat.Revived++;
            }
            Events.Add(record);
        }

        public void RegisterMinorExperience(MinorExperienceEventRecord record)
        {
            switch (record.type)
            {
                case MinorExperienceEventRecord.ExperienceType.Assist:
                    _stat.Assists++;
                    break;
                case MinorExperienceEventRecord.ExperienceType.MAXRepair:
                    _stat.MAXRepairs++;
                    break;
                case MinorExperienceEventRecord.ExperienceType.Resupply:
                    _stat.Resupplies++;
                    break;
                case MinorExperienceEventRecord.ExperienceType.Heal:
                    _stat.Heals++;
                    break;
                default:
                    break;
            }
            Events.Add(record);
        }
    }

}
