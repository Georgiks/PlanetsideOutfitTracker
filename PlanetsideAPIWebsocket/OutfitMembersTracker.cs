using JsonParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    class OutfitMembersTracker
    {
        // get player info by id
        //http://census.daybreakgames.com/s:georgik/get/ps2/character/?character_id=8272972087771354001&c:hide=certs,times,daily_ribbon,profile_id,head_id,title_id&c:join=outfit_member_extended^show:alias%27outfit_id%27name^inject_at:outfit_member

        ClientWebSocket StreamingAPISocket;
        Dictionary<JsonString, OutfitMemberStatRecorder> StatRecorders { get; } = new Dictionary<JsonString, OutfitMemberStatRecorder>();
        JsonString OutfitId { get; }
        JsonString OutfitName { get; }
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        CancellationToken token;
        Stream logFileStream;
        StreamWriter logFileWriter;

        Task socketListenTask;
        int eventProcessingsInProgress = 0;

        public OutfitMembersTracker(string alias, string logfile)
        {
            token = tokenSource.Token;

            string outfitQuerry = $@"https://census.daybreakgames.com/s:georgik/get/ps2/outfit?alias_lower={alias.ToLower()}&c:show=outfit_id,name&c:join=type:outfit_member^on:outfit_id^list:1^inject_at:members^show:character_id%27rank%27rank_ordinal(character^show:name^inject_at:character(characters_online_status^inject_at:onlineStatus^hide:character_id))";
            Console.WriteLine("Outfit data request sent");
            var outfitTask = PS2APIUtils.RestAPIRequest(outfitQuerry);
            Task establishSocketTask;

            List<JsonObject> allMembers = new List<JsonObject>();
            int onlineCount = 0;
            //List<JsonString> onlineMembers = new List<JsonString>();

            JsonObject response = outfitTask.GetAwaiter().GetResult();
            Console.WriteLine("Outfit data response received");
            
            try
            {
                JsonArray found = (JsonArray)response["outfit_list"];
                if (found.Length > 0)
                {
                    establishSocketTask = EstablishWebsocketConnection();

                    var outfit = found[0];
                    OutfitId = (JsonString)outfit["outfit_id"];
                    OutfitName = (JsonString)outfit["name"];
                    JsonArray members = (JsonArray)outfit["members"];

                    for (int i = 0; i < members.Length; i++)
                    {
                        var id = (JsonString)members?[i]?["character_id"];
                        var name = (JsonString)members?[i]?["character"]?["name"]?["first"];
                        var rank = (JsonString)members?[i]?["rank"];
                        var online = (JsonString)members?[i]?["character"]?["onlineStatus"]?["online_status"];

                        if (id == null) continue;
                        allMembers.Add(id);

                        var playerStats = new OutfitMemberStatRecorder(id, name, rank, OutfitId);
                        StatRecorders.Add(id, playerStats);
                        if (online != null && online.InnerString != "0")
                        {
                            playerStats.SetOnline(null);
                            onlineCount++;
                        }
                    }
                } else
                {
                    Console.WriteLine($"Outfit with tag '{alias}' not found!");
                    return;
                }
            }
            catch (InvalidCastException)
            {
                throw new JsonInvalidAccessException("Could not extract outfit info");
            }

            Console.WriteLine($"Outfit ID: {OutfitId}");
            Console.WriteLine($"Outfit Name: {OutfitName}");
            Console.WriteLine($"Total members registered: {allMembers.Count}");
            Console.WriteLine($"Online members: {onlineCount}");
            establishSocketTask.GetAwaiter().GetResult();
            WebsocketSendAllSubscribe(allMembers).GetAwaiter().GetResult();
            Console.WriteLine("Subscribed to all");

            logFileStream = Stream.Synchronized(new FileStream(logfile + ".csv", FileMode.Create));
            logFileWriter = new StreamWriter(logFileStream);
            logFileWriter.WriteLine(EventRecord.LogStringHeader);

            Console.WriteLine("Going to listen...");
            //WebsocketStreamListener().GetAwaiter().GetResult();
            //Task.Run(async () => await WebsocketStreamListener());
            socketListenTask = WebsocketStreamListener();
        }

        async Task WebsocketSendAllSubscribe(List<JsonObject> players)
        {
            List<JsonObject> events = new List<JsonObject>();
            events.Add(new JsonString("Death"));
            events.Add(new JsonString("PlayerLogin"));
            events.Add(new JsonString("PlayerLogout"));
            events.Add(new JsonString("VehicleDestroy"));
            events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdRevive));
            events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdSquadRevive));
            events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdResupply));
            events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdSquadResupply));
            events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdHeal));
            events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdSquadHeal));
            events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdKillAssist));
            events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdPriorityKillAssist));
            events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdHighPriorityKillAssist));
            events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdMAXRepair));
            events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdSquadMAXRepair));
            //events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdSpotKill));
            //events.Add(PS2APIConstants.ExpEvent(PS2APIConstants.ExperienceIdSquadSpotKill));
            string subscribeRequest = PS2APIEventUtils.GetSubscribeEvent(new JsonArray(players), new JsonArray(events)).ToString();
            await StreamingAPISocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(subscribeRequest)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        async Task WebsocketStreamListener()
        {
            var buffer = new ArraySegment<byte>(new byte[2048]);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            result = await StreamingAPISocket.ReceiveAsync(buffer, token);
                            ms.Write(buffer.Array, buffer.Offset, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close) break;

                        ms.Seek(0, SeekOrigin.Begin);

                        var msg = JsonObject.ParseFromStream(ms);
                        Interlocked.Increment(ref eventProcessingsInProgress);
                        // Without void return method, compiler is complaining, but I need it so I can make ContinueWith task
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        ProcessEventMessage(msg).ContinueWith((task) => Interlocked.Decrement(ref eventProcessingsInProgress));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                }
            } catch (AggregateException e)
            {
                foreach (var i in e.InnerExceptions)
                {
                    if (i is TaskCanceledException || i is OperationCanceledException)
                    {
                        Console.WriteLine("API listening cancelled by user request");
                    }
                }
                Console.WriteLine(e.Message);
            } catch (OperationCanceledException e) {
                Console.WriteLine("API listening cancelled by user request");
                Console.WriteLine(e.Message);
            } catch (WebSocketException e)
            {
                Console.WriteLine("Receiving from socket failed");
                Console.WriteLine(e.ToString());
            }
            finally
            {
                Console.WriteLine("Closing socket...");
                await StreamingAPISocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "User finished gathering data", CancellationToken.None);
                StreamingAPISocket.Dispose();
                Console.WriteLine("Streaming socket closed");
            }

        }

        public void FinishGathering()
        {
            tokenSource.Cancel();
            socketListenTask.GetAwaiter().GetResult();
            while (eventProcessingsInProgress > 0)
            {
                Console.WriteLine($"Waiting for events to finish: {eventProcessingsInProgress}");
                Thread.Sleep(3000);
            }
            Console.WriteLine("Waiting for remaining events finished!");
            logFileWriter.Dispose();
            logFileStream.Dispose();
            //DumpData();
        }

        async Task EstablishWebsocketConnection()
        {
            StreamingAPISocket = new ClientWebSocket();
            await StreamingAPISocket.ConnectAsync(new Uri("wss://push.planetside2.com/streaming?environment=ps2&service-id=s:georgik"), CancellationToken.None);
            Console.WriteLine("Connected to socket");
        }

        public async Task<KillEventRecord> ProcessDeathRecord(JsonObject payload)
        {
            OutfitMemberStatRecorder person;
            KillEventRecord killRecord = await KillEventRecord.Parse(payload);

            if (killRecord.attacker.Id != null && StatRecorders.TryGetValue(killRecord.attacker.Id, out person))
            {
                lock (person) person.RegisterDeathEvent(killRecord);
            }
            if (killRecord.victim.Id != null && killRecord.attacker.Id != killRecord.victim.Id && StatRecorders.TryGetValue(killRecord.victim.Id, out person))
            {
                lock (person) person.RegisterDeathEvent(killRecord);
            }
            return killRecord;
        }
        public async Task<ReviveEventRecord> ProcessReviveRecord(JsonObject payload, string xpId)
        {
            OutfitMemberStatRecorder person;
            ReviveEventRecord expRecord;

            if (xpId == PS2APIConstants.ExperienceIdSquadRevive)
                expRecord = await ReviveEventRecord.Parse<SquadReviveEventRecord>(payload);
            else
                expRecord = await ReviveEventRecord.Parse<NonSquadReviveEventRecord>(payload);


            if (expRecord.reviver.Id != null && StatRecorders.TryGetValue(expRecord.reviver.Id, out person))
            {
                lock (person) person.RegisterRevive(expRecord);
            }
            if (expRecord.revived.Id != null && expRecord.revived.Id != expRecord.reviver.Id && StatRecorders.TryGetValue(expRecord.revived.Id, out person))
            {
                lock (person) person.RegisterRevive(expRecord);
            }
            return expRecord;
        }
        public async Task<PlayerLoginEventRecord> ProcessLoginRecord(JsonObject payload)
        {
            OutfitMemberStatRecorder person;
            PlayerLoginEventRecord loginRecord = await PlayerLoggingEventRecord.Parse<PlayerLoginEventRecord>(payload);

            if (loginRecord.character.Id != null && StatRecorders.TryGetValue(loginRecord.character.Id, out person))
            {
                lock (person) person.SetOnline(loginRecord);
            }
            return loginRecord;
        }
        public async Task<PlayerLogoutEventRecord> ProcessLogoutRecord(JsonObject payload)
        {
            OutfitMemberStatRecorder person;
            PlayerLogoutEventRecord logoutRecord = await PlayerLoggingEventRecord.Parse<PlayerLogoutEventRecord>(payload);

            if (logoutRecord.character.Id != null && StatRecorders.TryGetValue(logoutRecord.character.Id, out person))
            {
                lock (person) person.SetOffline(logoutRecord);
            }
            return logoutRecord;
        }
        public async Task<VehicleDestroyedEventRecord> ProcessVehicleDestroyRecord(JsonObject payload)
        {
            OutfitMemberStatRecorder person;
            VehicleDestroyedEventRecord vehicleRecord = await VehicleDestroyedEventRecord.Parse(payload);

            if (vehicleRecord.attacker.Id != null && StatRecorders.TryGetValue(vehicleRecord.attacker.Id, out person))
            {
                lock (person) person.RegisterVehicleDestroyed(vehicleRecord);
            }
            if (vehicleRecord.victim.Id != null && vehicleRecord.victim.Id != vehicleRecord.attacker.Id && StatRecorders.TryGetValue(vehicleRecord.victim.Id, out person))
            {
                lock (person) person.RegisterVehicleDestroyed(vehicleRecord);
            }
            return vehicleRecord;
        }
        public async Task<MinorExperienceEventRecord> ProcessMinorExperienceRecord(JsonObject payload)
        {
            OutfitMemberStatRecorder person;
            MinorExperienceEventRecord record = await MinorExperienceEventRecord.Parse(payload);
            if (record.character.Id != null && StatRecorders.TryGetValue(record.character.Id, out person))
            {
                lock (person) person.RegisterMinorExperience(record);
            }
            return record;
        }


        async Task ProcessEventMessage(JsonObject message)
        {
            string type = (message["type"] as JsonString)?.InnerString;
            if (type == null || type != "serviceMessage") return;

            var payload = message["payload"];
            string eventType = (payload["event_name"] as JsonString)?.InnerString;
            if (eventType == null) Console.WriteLine($"Event type not found! {message.ToString()}");

            //Program.Logger.Log("something", long.Parse((payload["timestamp"] as JsonString).InnerString));
            //return;

            EventRecord record;
            switch (eventType)
            {
                case "Death":
                    record = await ProcessDeathRecord(payload);
                    break;
                case "GainExperience":
                    string xpId = (payload?["experience_id"] as JsonString)?.InnerString;
                    if (xpId == PS2APIConstants.ExperienceIdRevive || xpId == PS2APIConstants.ExperienceIdSquadRevive)
                    {
                        record = await ProcessReviveRecord(payload, xpId);
                    }
                    else if (MinorExperienceEventRecord.GetExperienceType(xpId) != MinorExperienceEventRecord.ExperienceType.Unknown) {
                        record = await ProcessMinorExperienceRecord(payload);
                    }
                    else
                    {
                        Program.Logger.Log($"Received unknown experience gained! {xpId}");
                        record = null;
                    }
                    break;
                case "PlayerLogin":
                    record = await ProcessLoginRecord(payload);
                    break;
                case "PlayerLogout":
                    record = await ProcessLogoutRecord(payload);
                    break;
                case "VehicleDestroy":
                    record = await ProcessVehicleDestroyRecord(payload);
                    break;
                default:
                    Console.WriteLine($"Unknown event type! {eventType}");
                    record = null;
                    break;
            }

            if (record != null/* && !(record is MinorExperienceEventRecord)*/)
            {
                Program.Logger.Log(record.ToString(), record.timestamp);
            }
            if (record != null)
            {
                await logFileWriter.WriteLineAsync(record.GetLogString());
            }
        }
        
        public void DumpData()
        {
            Console.WriteLine(GetPlayerStatistics());
        }
        
        public string GetPlayerStatistics()
        {
            StringBuilder sb = new StringBuilder();
            string[] statNames = Enum.GetNames(typeof(OutfitMemberStatRecorder.Statistic));
            sb.AppendLine("Name;Rank;Online (minutes);" + string.Join(";", statNames));
            foreach (var i in StatRecorders)
            {
                if (i.Value.OnlineTime.Ticks == 0) continue;
                i.Value.AppendData(sb);
            }
            return sb.ToString();
        }
    }


    class OutfitMemberStatRecorder
    {
        public enum Statistic {
            Kills,
            ConventionalKills,
            Deaths,
            Assists,
            Teamkills,
            Outfitkills,
            Teamdeaths,
            Outfitdeaths,
            Revives,
            SquadRevives,
            OutfitRevives,
            SelfRevives,
            Suicides,
            Revived,
            VehiclesDestroyed,
            VehiclesLost,
            VehiclesSelfDestroyed,
            TeamVehiclesDestroyed,
            OutfitVehiclesDestroyed,
            HeadshotEnemyKills,
            MAXRepairs,
            Heals,
            //SpotAssists,
            ResuppliesPlayer
        }
        Dictionary<Statistic, int> StatsDictionary { get; } = new Dictionary<Statistic, int>();
        SortedSet<EventRecord> Events { get; } = new SortedSet<EventRecord>(Comparer<EventRecord>.Create((a,b) => (int)(a.timestamp - b.timestamp)));
        JsonString CharacterId { get; }
        JsonString CharacterName { get; }
        JsonString OutfitRank { get; }
        JsonString OutfitId { get; }

        TimeSpan OnlineTimespan = new TimeSpan(0);
        DateTime OnlineSince = new DateTime(0);

        private void RegisterStatisticChange(Statistic stat)
        {
            int value;
            if (!StatsDictionary.TryGetValue(stat, out value))
            {
                value = 0;
            }
            StatsDictionary[stat] = ++value;
        }


        public TimeSpan OnlineTime
        {   
            get
            {
                if (OnlineSince.Ticks > 0) return OnlineTimespan + (DateTime.Now - OnlineSince);
                else return OnlineTimespan;
            }
        }
        public void PrintMyData()
        {
            StringBuilder sb = new StringBuilder();
            AppendData(sb);
            Console.Write(sb.ToString());
            //Console.WriteLine($"{CharacterName};{OutfitRank};{OnlineTime.TotalMinutes};{kills / (float)deaths};{kills};{teamkills};{outfitkills};{deaths};{teamdeaths};{outfitdeaths};{revives};{squadrevives};{outfitrevives};{selfrevives};{suicides};{revived};{vehiclesDestroyed};{vehiclesLost};{vehiclesSelfdestroyed};{teamVehicleDestroyed};{outfitVehicleDestroyed};{headshotKills}");
        }
        public void AppendData(StringBuilder sb)
        {
            var values = Enum.GetValues(typeof(Statistic));
            sb.Append(CharacterName).Append(';').Append(OutfitRank).Append(';').Append(OnlineTime.TotalMinutes).Append(';');
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(';');
                var v = (Statistic)values.GetValue(i);
                int stat;
                if (!StatsDictionary.TryGetValue(v, out stat))
                {
                    stat = 0;
                }
                sb.Append(stat);
            }
            sb.AppendLine();
        }

        public OutfitMemberStatRecorder(JsonString id, JsonString name, JsonString rank, JsonString outfit)
        {
            CharacterId = id;
            CharacterName = name;
            OutfitRank = rank;
            OutfitId = outfit;
        }
        public void SetOnline(PlayerLoginEventRecord record)
        {
            if (record == null) record = new PlayerLoginEventRecord() { timestamp = DateTimeOffset.Now.ToUniversalTime().ToUnixTimeSeconds(), character = new NameOutfitFactionRecord() { Name = CharacterName?.InnerString, Id = CharacterId } };
            DateTime datetime = DateTimeOffset.FromUnixTimeSeconds(record.timestamp).UtcDateTime.ToLocalTime();

            if (OnlineSince.Ticks > 0)
            {
                Console.WriteLine($"{record.character.Name} is ONLINE but there is already record of him being online!");
                
                OnlineTimespan += datetime - OnlineSince;
            }
            OnlineSince = datetime;
            Events.Add(record);
        }
        public void SetOffline(PlayerLogoutEventRecord record)
        {
            if (record == null) record = new PlayerLogoutEventRecord() { timestamp = DateTimeOffset.Now.ToUniversalTime().ToUnixTimeSeconds(), character = new NameOutfitFactionRecord() { Name = CharacterName?.InnerString, Id = CharacterId } };
            var datetime = DateTimeOffset.FromUnixTimeSeconds(record.timestamp).UtcDateTime.ToLocalTime();

            if (OnlineSince.Ticks == 0)
            {
                Console.WriteLine($"{record.character.Name} is OFFLINE but was not marked as online before!");
            }
            else
            {
                OnlineTimespan += datetime - OnlineSince;
            }
            OnlineSince = new DateTime(0);
            Events.Add(record);
        }

        public void RegisterDeathEvent(KillEventRecord record)
        {
            if (record.attacker.Id == record.victim.Id)
            {
                RegisterStatisticChange(Statistic.Suicides);
            }
            else if (record.attacker.Id == CharacterId)
            {
                if (record.victim.Faction != record.attacker.Faction) RegisterStatisticChange(Statistic.Kills);
                else RegisterStatisticChange(Statistic.Teamkills);
                if (record.victim.Outfit == record.attacker.Outfit) RegisterStatisticChange(Statistic.Outfitkills);
                if (record.headshot && record.victim.Faction != record.attacker.Faction) RegisterStatisticChange(Statistic.HeadshotEnemyKills);
                if (record.victim.Faction != record.attacker.Faction && record.weaponName != null && record.weaponName.IndexOf("Orbital Strike", StringComparison.OrdinalIgnoreCase) == -1 && record.attackerVehicle.Name == null)
                    RegisterStatisticChange(Statistic.ConventionalKills);
            }
            else
            {
                if (record.attacker.Faction != record.victim.Faction) RegisterStatisticChange(Statistic.Deaths);
                else RegisterStatisticChange(Statistic.Teamdeaths);
                if (record.attacker.Outfit == record.victim.Outfit) RegisterStatisticChange(Statistic.Outfitdeaths);
            }
            Events.Add(record);
        }
        public void RegisterVehicleDestroyed(VehicleDestroyedEventRecord record)
        {
            if (record.destroyedVehicle.Type?.InnerString != PS2APIConstants.TurretVehicleTypeId)
            {
                if (record.attacker.Id == record.victim.Id)
                {
                    RegisterStatisticChange(Statistic.VehiclesSelfDestroyed);
                }
                else if (record.attacker.Id == CharacterId)
                {
                    if (record.victim.Faction != record.attacker.Faction) RegisterStatisticChange(Statistic.VehiclesDestroyed);
                    else RegisterStatisticChange(Statistic.TeamVehiclesDestroyed);
                    if (record.victim.Outfit == record.attacker.Outfit) RegisterStatisticChange(Statistic.OutfitVehiclesDestroyed);
                }
                if (record.victim.Id == CharacterId)
                {
                    RegisterStatisticChange(Statistic.VehiclesLost);
                }
            }

            Events.Add(record);
        }
        public void RegisterRevive(ReviveEventRecord record)
        {
            if (record.reviver.Id == record.revived.Id)
            {
                RegisterStatisticChange(Statistic.SelfRevives);
            }
            else if (record.reviver.Id == CharacterId)
            {
                if (record is SquadReviveEventRecord) RegisterStatisticChange(Statistic.SquadRevives);
                else RegisterStatisticChange(Statistic.Revives);
                if (record.revived.Outfit == record.reviver.Outfit) RegisterStatisticChange(Statistic.OutfitRevives);
            }
            else
            {
                RegisterStatisticChange(Statistic.Revived);
            }
            Events.Add(record);
        }

        public void RegisterMinorExperience(MinorExperienceEventRecord record)
        {
            switch (record.type)
            {
                case MinorExperienceEventRecord.ExperienceType.Assist:
                    RegisterStatisticChange(Statistic.Assists);
                    break;
                case MinorExperienceEventRecord.ExperienceType.MAXRepair:
                    RegisterStatisticChange(Statistic.MAXRepairs);
                    break;
                case MinorExperienceEventRecord.ExperienceType.Resupply:
                    RegisterStatisticChange(Statistic.ResuppliesPlayer);
                    break;
                case MinorExperienceEventRecord.ExperienceType.Heal:
                    RegisterStatisticChange(Statistic.Heals);
                    break;
                //case MinorExperienceEventRecord.ExperienceType.SpotAssist:
                //    RegisterStatisticChange(Statistic.SpotAssists);
                //    break;
                default:
                    break;
            }
            // Minor events are not shown in event log ?
            Events.Add(record);
        }
    }
}