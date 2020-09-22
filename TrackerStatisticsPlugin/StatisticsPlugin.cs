using JsonParser;
using PlanetsideAPIWebsocket;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace TrackerStatisticsPlugin
{
    /// <summary>
    /// Plugin to OutfitMemberTracker which tracks performance statistics of each player, and at the end of tracking, shows simple-ish
    /// UI WPF window and add possibility to save stats to CSV file.
    /// </summary>
    public class StatisticsPlugin : IPlugin
    {
        string sessionName;
        EventRecordsHandler handler;
        DateTime trackingEndTime;

        public void Init(OutfitMembersTracker tracker, string sessionName)
        {
            handler = new EventRecordsHandler(tracker.Members);
            // register EventRecordsHandler to the Tracker
            tracker.AddHandler(handler);

            this.sessionName = sessionName;
        }

        void StartUI()
        {
            Application app = new Application();
            TrackerWindow mainWindow = new TrackerWindow(handler, SaveStats, SaveAllRecords, sessionName);

            app.Run(mainWindow);

        }

        public void TrackingEnded()
        {
            trackingEndTime = DateTime.Now;

            // we crate UI window, but do it in another thread! This original thread can be still used by other plugins
            Thread uiThread = new Thread(StartUI);
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();
        }

        /// <summary>
        /// Contains information about record's attributes to be saved into csv file
        /// </summary>
        struct StatsSaveRecord
        {
            public string Name;
            public Func<PlayerStats, string> Value;
        }

        /// <summary>
        /// Saves statistics into csv file
        /// </summary>
        /// <param name="fileName">Name of the csv file</param>
        void SaveStats(string fileName)
        {
            // semicolon may be better but it's less common than comma. Just make sure number fraction separators are not commas too
            const char csvSeparator = ',';

            // make a list of all stats and their name in one list
            // format of the resulting file is directly dependent on ordering of this list
            List<StatsSaveRecord> statsAccessor = new List<StatsSaveRecord>()
            {
                new StatsSaveRecord() { Name = "Id", Value = (s) => s.CharacterId.InnerString.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Name", Value = (s) => s.CharacterName.InnerString.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Rank", Value = (s) => s.OutfitRank.InnerString.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Online (minutes)", Value = (s) => s.OnlineTimeUntil(trackingEndTime).TotalMinutes.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Kills", Value = (s) => s.Stats.Kills.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Conventional kills", Value = (s) => s.Stats.ConventionalKills.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Deaths", Value = (s) => s.Stats.Deaths.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Assists", Value = (s) => s.Stats.Assists.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Teamkills", Value = (s) => s.Stats.TeamKills.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Teamdeaths", Value = (s) => s.Stats.TeamDeaths.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Suicides", Value = (s) => s.Stats.Suicides.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Outfitkills", Value = (s) => s.Stats.OutfitKills.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Outfitdeaths", Value = (s) => s.Stats.OutfitDeaths.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Vehicles destroyed", Value = (s) => s.Stats.VehiclesDestroyed.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Vehicles lost", Value = (s) => s.Stats.VehiclesLost.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Vehicles suicide", Value = (s) => s.Stats.VehiclesSelfDestroyed.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Team vehicles destroyed", Value = (s) => s.Stats.TeamVehiclesDestroyed.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Outfit vehicles destroyed", Value = (s) => s.Stats.OutfitVehiclesDestroyed.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Headshots", Value = (s) => s.Stats.HeadshotEnemyKills.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Revives", Value = (s) => s.Stats.Revives.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Squad revives", Value = (s) => s.Stats.SquadRevives.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Outfit revives", Value = (s) => s.Stats.OutfitRevives.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Revived", Value = (s) => s.Stats.Revived.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Heals", Value = (s) => s.Stats.Heals.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "MAX repairs", Value = (s) => s.Stats.MAXRepairs.ToString(CultureInfo.InvariantCulture) },
                new StatsSaveRecord() { Name = "Resupplies", Value = (s) => s.Stats.Resupplies.ToString(CultureInfo.InvariantCulture) },
            };

            using (FileStream fs = File.Open(fileName, FileMode.Create))
            using (StreamWriter sw = new StreamWriter(fs))
            {
                // write header
                for (int i = 0; i < statsAccessor.Count; i++)
                {
                    sw.Write(statsAccessor[i].Name);
                    if (i < statsAccessor.Count - 1)
                        sw.Write(csvSeparator);
                }
                sw.WriteLine();

                // write stats for each player
                foreach (var pair in handler.Players)
                {
                    if (pair.Value.OnlineTime.Ticks == 0)
                        continue;

                    for (int i = 0; i < statsAccessor.Count; i++)
                    {
                        sw.Write(statsAccessor[i].Value(pair.Value));
                        if (i < statsAccessor.Count - 1)
                            sw.Write(csvSeparator);
                    }
                    sw.WriteLine();
                }
            }
        }

        /// <summary>
        /// Saving all records into csv file.
        /// </summary>
        /// <param name="fileName">name of the csv file</param>
        void SaveAllRecords(string fileName)
        {
            List<EventRecord> allRecords = new List<EventRecord>();
            foreach (var pair in handler.Players)
            {
                allRecords.AddRange(pair.Value.Events);
            }

            allRecords.Sort((a,b) => Comparer<long>.Default.Compare(a.timestamp, b.timestamp));

            using (FileStream fs = File.Open(fileName, FileMode.Create))
            using (StreamWriter sw = new StreamWriter(fs))
            {
                sw.WriteLine(EventRecord.LogStringHeader);
                foreach (var record in allRecords)
                {
                    sw.WriteLine(record.GetLogString());
                }
            }
        }
    }
}
