using JsonParser;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    /// <summary>
    /// Main class for receiving, processing and dispatching event records from Planetside2 API
    /// </summary>
    public class OutfitMembersTracker
    {
        /// <summary>
        /// Records of outfit member
        /// </summary>
        public struct Member
        {
            public JsonString Name;
            public JsonString Rank;
            public JsonString Id;
        }

        /// <summary>
        /// Socket used to receive streaming API events
        /// </summary>
        ClientWebSocket StreamingAPISocket;

        /// <summary>
        /// Outfit database Id
        /// </summary>
        public JsonString OutfitId { get; }

        /// <summary>
        /// Outfit full name
        /// </summary>
        public JsonString OutfitName { get; }

        /// <summary>
        /// Outfit tag (usually 3-4 letters)
        /// </summary>
        public JsonString OutfitAlias { get; }

        // token used to finish tracking
        CancellationTokenSource tokenSource = new CancellationTokenSource();
        CancellationToken token;
        
        Task socketListenTask;
        int eventProcessingsInProgress = 0;

        ReaderWriterLockSlim handlersLock = new ReaderWriterLockSlim();
        HashSet<IEventRecordHandler> handlers = new HashSet<IEventRecordHandler>();

        public List<Member> Members { get; } = new List<Member>();

        /// <summary>
        /// Adds handlers for EventRecords (thread safe)
        /// </summary>
        public void AddHandler(IEventRecordHandler handler)
        {
            handlersLock.EnterWriteLock();
            try
            {
                handlers.Add(handler);
            } finally
            {
                handlersLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes handler for EventRecords (thread safe)
        /// </summary>
        public void RemoveHandler(IEventRecordHandler handler) {
            handlersLock.EnterWriteLock();
            try
            {
                handlers.Remove(handler);
            }
            finally
            {
                handlersLock.ExitWriteLock();
            }
        }



        public OutfitMembersTracker(string alias, string logfile)
        {
            OutfitAlias = new JsonString(alias);

            token = tokenSource.Token;

            // get information about outfit members
            string outfitQuerry = $@"https://census.daybreakgames.com/s:{PS2APIConstants.ServiceId}/get/ps2/outfit?alias_lower={alias.ToLower()}&c:show=outfit_id,name&c:join=type:outfit_member^on:outfit_id^list:1^inject_at:members^show:character_id%27rank%27rank_ordinal(character^show:name^inject_at:character(characters_online_status^inject_at:onlineStatus^hide:character_id))";
            Console.WriteLine("Outfit data request sent");
            var outfitTask = PS2APIUtils.RestAPIRequestClient(outfitQuerry);

            int onlineCount = 0;

            JsonObject response = outfitTask.GetAwaiter().GetResult();
            if (response == null)
            {
                throw new Exception("Outfit data request failed");
            }
            Console.WriteLine("Outfit data response received");
            
            try
            {
                JsonArray found = (JsonArray)response["outfit_list"];
                if (found == null)
                {
                    throw new JsonInvalidAccessException("Outfit list wrong format?\n" + response.ToString());
                }

                if (found.Length > 0)
                {
                    var outfit = found[0];
                    OutfitId = (JsonString)outfit["outfit_id"];
                    OutfitName = (JsonString)outfit["name"];
                    JsonArray members = (JsonArray)outfit["members"];

                    for (int i = 0; i < members.Length; i++)
                    {
                        var id = (JsonString)members?[i]?["character_id"];
                        if (id == null || id.InnerString == "0")
                            continue;
                        var name = (JsonString)members?[i]?["character"]?["name"]?["first"];
                        var rank = (JsonString)members?[i]?["rank"];
                        var online = (JsonString)members?[i]?["character"]?["onlineStatus"]?["online_status"];

                        Members.Add(new Member() { Name = name, Rank = rank, Id = id});

                        if (online != null && online.InnerString != "0")
                        {
                            onlineCount++;
                        }
                    }
                } else
                {
                    throw new Exception($"Outfit with tag '{alias}' not found!");
                }
            }
            catch (InvalidCastException)
            {
                throw new JsonInvalidAccessException("Could not extract outfit info");
            }

            Console.WriteLine($"Outfit ID: {OutfitId}");
            Console.WriteLine($"Outfit Name: {OutfitName}");
            Console.WriteLine($"Total members registered: {Members.Count}");
            Console.WriteLine($"Online members: {onlineCount}");
            
        }

        /// <summary>
        /// Non-blocking start of serving web socket
        /// </summary>
        internal void StartListening()
        {
            Task establishSocketTask = EstablishWebsocketConnection();
            establishSocketTask.GetAwaiter().GetResult();
            WebsocketSendSubscribeAll(Members.Select((m) => (JsonObject)m.Id).ToList()).GetAwaiter().GetResult();

            Console.WriteLine("Going to listen...");
            socketListenTask = WebsocketStreamListener();
        }

        /// <summary>
        /// Sends message to stream websocket registering for all supported events
        /// </summary>
        async Task WebsocketSendSubscribeAll(List<JsonObject> players)
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
            string subscribeRequest = PS2APIEventUtils.GetSubscribeEvent(new JsonArray(players), new JsonArray(events)).ToString();
            await StreamingAPISocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(subscribeRequest)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        /// <summary>
        /// Main listening & serving loop
        /// </summary>
        async Task WebsocketStreamListener()
        {
            var buffer = new ArraySegment<byte>(new byte[2048]);
            Queue<string> lastMsgs = new Queue<string>(10);

            try
            {
                // check whether we want to finish before processing new message
                while (!token.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    using (var ms = new MemoryStream())
                    {
                        // receive from socket until whole message is received
                        do
                        {
                            result = await StreamingAPISocket.ReceiveAsync(buffer, token);
                            ms.Write(buffer.Array, buffer.Offset, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close) break;

                        var sr = new StreamReader(ms);
                        ms.Seek(0, SeekOrigin.Begin);
                        string stringMsg = sr.ReadToEnd();

                        // The API is bugged and sends same data twice, we try to compensate for that
                        if (lastMsgs.Contains(stringMsg))
                        {
                            // same data received 
                            continue;
                        }
                        if (lastMsgs.Count == 10) lastMsgs.Dequeue();
                        lastMsgs.Enqueue(stringMsg);

                        ms.Seek(0, SeekOrigin.Begin);

                        var msg = JsonObject.ParseFromStream(ms);

                        // start new async task with our message
                        StartProcessMsg(msg);
                    }
                }
            } catch (AggregateException e)
            {
                foreach (var i in e.InnerExceptions)
                {
                    if (i is TaskCanceledException || i is OperationCanceledException)
                    {
                        Console.WriteLine("API listening cancelled by user request (Aggregate)");
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

        /// <summary>
        /// Async task handling received event message from stream websocket
        /// </summary>
        /// <param name="msg"></param>
        async void StartProcessMsg(JsonObject msg) 
        {
            // general timeout for processing of one message
            int timeout = 60_000;
            // increment global counter of currently processed events (used when terminating tracking, we wait until ëvery message is finished)
            Interlocked.Increment(ref eventProcessingsInProgress);
            try
            {
                Task t = ProcessEventMessage(msg);
                if (await Task.WhenAny(t, Task.Delay(timeout)) == t)
                {
                    await t;
                }
                else
                {
                    Console.WriteLine("Task timed out!");
                    Console.WriteLine(msg.ToString());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Task threw exception!");
                Console.WriteLine(e);
            }
            finally
            {
                // finished processing the event
                Interlocked.Decrement(ref eventProcessingsInProgress);
            }
        }

        /// <summary>
        /// Terminates tracking info, breaking main loop. Socket is closed and disposed in the main loop's method
        /// This method basically activates cancellation token and waits until all events are finished
        /// </summary>
        internal void Finish()
        {
            tokenSource.Cancel();
            socketListenTask?.GetAwaiter().GetResult();
            long lastInProgress = eventProcessingsInProgress;

            int pendingTimeout = 0;
            while (eventProcessingsInProgress > 0)
            {
                Console.WriteLine($"Waiting for events to finish: {eventProcessingsInProgress}");
                Thread.Sleep(1000);

                if (eventProcessingsInProgress == lastInProgress)
                {
                    pendingTimeout++;
                    if (pendingTimeout >= 5)
                    {
                        Console.WriteLine("Waiting timed out!");
                        break;
                    }
                } else
                {
                    pendingTimeout = 0;
                    lastInProgress = eventProcessingsInProgress;
                }
            }
            Console.WriteLine("Waiting for remaining events finished!");
        }

        /// <summary>
        /// Creates web socket connection
        /// </summary>
        async Task EstablishWebsocketConnection()
        {
            StreamingAPISocket = new ClientWebSocket();
            await StreamingAPISocket.ConnectAsync(new Uri($"wss://push.planetside2.com/streaming?environment=ps2&service-id=s:{PS2APIConstants.ServiceId}"), CancellationToken.None);
            Console.WriteLine("Connected to socket");
        }

        async Task<KillEventRecord> ProcessDeathRecord(JsonObject payload)
        {
            KillEventRecord killRecord = await KillEventRecord.Parse(payload);
            
            foreach (var handler in handlers)
            {
                handlersLock.EnterReadLock();
                try
                {
                    handler.Handle(killRecord);
                } finally
                {
                    handlersLock.ExitReadLock();
                }
            }
            return killRecord;
        }
        async Task<ReviveEventRecord> ProcessReviveRecord(JsonObject payload, string xpId)
        {
            ReviveEventRecord expRecord = await ReviveEventRecord.Parse(payload, xpId == PS2APIConstants.ExperienceIdSquadRevive);

            foreach (var handler in handlers)
            {
                handlersLock.EnterReadLock();
                try
                {
                    handler.Handle(expRecord);
                }
                finally
                {
                    handlersLock.ExitReadLock();
                }
            }
            return expRecord;
        }
        async Task<PlayerLoginEventRecord> ProcessLoginRecord(JsonObject payload)
        {
            PlayerLoginEventRecord loginRecord = await PlayerLoggingEventRecord.Parse<PlayerLoginEventRecord>(payload);

            foreach (var handler in handlers)
            {
                handlersLock.EnterReadLock();
                try
                {
                    handler.Handle(loginRecord);
                }
                finally
                {
                    handlersLock.ExitReadLock();
                }
            }
            return loginRecord;
        }

        async Task<PlayerLogoutEventRecord> ProcessLogoutRecord(JsonObject payload)
        {
            PlayerLogoutEventRecord logoutRecord = await PlayerLoggingEventRecord.Parse<PlayerLogoutEventRecord>(payload);

            foreach (var handler in handlers)
            {
                handlersLock.EnterReadLock();
                try
                {
                    handler.Handle(logoutRecord);
                }
                finally
                {
                    handlersLock.ExitReadLock();
                }
            }
            return logoutRecord;
        }

        async Task<VehicleDestroyedEventRecord> ProcessVehicleDestroyRecord(JsonObject payload)
        {
            VehicleDestroyedEventRecord vehicleRecord = await VehicleDestroyedEventRecord.Parse(payload);

            foreach (var handler in handlers)
            {
                handlersLock.EnterReadLock();
                try
                {
                    handler.Handle(vehicleRecord);
                }
                finally
                {
                    handlersLock.ExitReadLock();
                }
            }
            return vehicleRecord;
        }

        async Task<MinorExperienceEventRecord> ProcessMinorExperienceRecord(JsonObject payload)
        {
            MinorExperienceEventRecord record = await MinorExperienceEventRecord.Parse(payload);

            foreach (var handler in handlers)
            {
                handlersLock.EnterReadLock();
                try
                {
                    handler.Handle(record);
                }
                finally
                {
                    handlersLock.ExitReadLock();
                }
            }

            return record;
        }

        /// <summary>
        /// Actual processing of the received message, identifying event type and further proceeding by that
        /// </summary>
        /// <param name="message"></param>
        async Task ProcessEventMessage(JsonObject message)
        {
            // check for only event message
            string type = (message["type"] as JsonString)?.InnerString;
            if (type == null || type != "serviceMessage") return;

            var payload = message["payload"];
            string eventType = (payload["event_name"] as JsonString)?.InnerString;

            // create particular event record and dispatch it further to consumers
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

            // print human readable output to console
            if (record != null/* && !(record is MinorExperienceEventRecord)*/)
            {
                Program.Logger.Log(record.ToString(), record.timestamp);
            }
        }

    }
}