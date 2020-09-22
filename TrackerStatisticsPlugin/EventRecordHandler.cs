using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonParser;
using PlanetsideAPIWebsocket;

namespace TrackerStatisticsPlugin
{
    /// <summary>
    /// Handler to dispatch various EventRecords produced by OutfitMemberTracker
    /// </summary>
    public class EventRecordsHandler : IEventRecordHandler
    {
        internal Dictionary<JsonString, PlayerStats> Players = new Dictionary<JsonString, PlayerStats>();

        public EventRecordsHandler(IEnumerable<OutfitMembersTracker.Member> members)
        {
            foreach (var member in members)
            {
                Players.Add(member.Id, new PlayerStats(member.Id, member.Name, member.Rank));
            }
        }


        public void Handle(PlayerLoginEventRecord record)
        {
            PlayerStats person;

            if (record.character.Id != null && Players.TryGetValue(record.character.Id, out person))
            {
                lock (person)
                    person.SetOnline(record);
            }
        }

        public void Handle(PlayerLogoutEventRecord record)
        {
            PlayerStats person;

            if (record.character.Id != null && Players.TryGetValue(record.character.Id, out person))
            {
                lock (person)
                    person.SetOffline(record);
            }
        }

        public void Handle(KillEventRecord record)
        {
            PlayerStats person;
            if (record.attacker.Id != null && Players.TryGetValue(record.attacker.Id, out person))
            {
                lock (person)
                    person.RegisterDeathEvent(record);
            }
            if (record.victim.Id != null && record.attacker.Id != record.victim.Id && Players.TryGetValue(record.victim.Id, out person))
            {
                lock (person)
                    person.RegisterDeathEvent(record);
            }
        }

        public void Handle(ReviveEventRecord record)
        {
            PlayerStats person;

            if (record.reviver.Id != null && Players.TryGetValue(record.reviver.Id, out person))
            {
                lock (person)
                    person.RegisterRevive(record);
            }
            if (record.revived.Id != null && record.revived.Id != record.reviver.Id && Players.TryGetValue(record.revived.Id, out person))
            {
                lock (person)
                    person.RegisterRevive(record);
            }
        }

        public void Handle(VehicleDestroyedEventRecord record)
        {
            PlayerStats person;

            if (record.attacker.Id != null && Players.TryGetValue(record.attacker.Id, out person))
            {
                lock (person)
                    person.RegisterVehicleDestroyed(record);
            }
            if (record.victim.Id != null && record.victim.Id != record.attacker.Id && Players.TryGetValue(record.victim.Id, out person))
            {
                lock (person)
                    person.RegisterVehicleDestroyed(record);
            }
        }

        public void Handle(MinorExperienceEventRecord record)
        {
            PlayerStats person;

            if (record.character.Id != null && Players.TryGetValue(record.character.Id, out person))
            {
                lock (person)
                    person.RegisterMinorExperience(record);
            }
        }
    }
}
