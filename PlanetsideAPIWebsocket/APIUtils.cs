using JsonParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    public static class PS2APIConstants
    {
        public static JsonString ExpEvent(string experienceId)
        {
            return new JsonString("GainExperience_experience_id_" + experienceId);
        }
        public const string ServiceId = "georgik";

        public const string MillerWorld = "10";
        public const string ExperienceIdRevive = "7";
        public const string ExperienceIdSquadRevive = "53";
        public const string UnknownWeaponTranslate = "deadly view";
        public const string TurretVehicleTypeId = "7";
        public const string ExperienceIdKillAssist = "2";
        public const string ExperienceIdPriorityKillAssist = "371";
        public const string ExperienceIdHighPriorityKillAssist = "372";
        public const string ExperienceIdResupply = "34";
        public const string ExperienceIdHeal = "4";
        public const string ExperienceIdSquadHeal = "51";
        public const string ExperienceIdSquadResupply = "55";
        //public const string ExperienceIdSpotKill = "36";
        //public const string ExperienceIdSquadSpotKill = "54";
        public const string ExperienceIdMAXRepair = "6";
        public const string ExperienceIdSquadMAXRepair = "142";
    }

    public static class PS2APIUtils
    {
        public static async Task<JsonObject> RestAPIRequest(string uriString, int timeoutMs = 15000)
        {
            StringBuilder debugSB = new StringBuilder();
            
            Uri uri = new Uri(uriString);
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);

            try
            {
                var waitTask = Task.Delay(timeoutMs).ContinueWith((t) => webRequest.Abort());
                var responseTask = webRequest.GetResponseAsync();

                using (WebResponse response = await responseTask)
                {
                    Stream stream = response.GetResponseStream();
                    MemoryStream ms = new MemoryStream();
                    using (StreamReader reader = new StreamReader(stream))
                    using (StreamWriter writer = new StreamWriter(ms))
                    {
                        string receivedString = await reader.ReadToEndAsync();
                        await writer.WriteAsync(receivedString);

                        writer.Flush();
                        ms.Position = 0;
                        try
                        {
                            return JsonObject.ParseFromStream(ms);
                        }
                        catch
                        {
                            Console.WriteLine(receivedString);
                            throw;
                        }
                    }
                }
            }
            catch (WebException e)
            {
                Console.WriteLine($"Request failed ({e.Status} - {e.Message}): {uriString}");
                if (e.Status == WebExceptionStatus.RequestCanceled) return null;
                // sometimes page returns error code (probably DB temp inaccessibility?)
                var exResponse = e.Response as HttpWebResponse;
                if (exResponse == null)
                {
                    Console.WriteLine("Response from server is null!");
                    return null;
                }
                var respStream = exResponse.GetResponseStream();
                if (respStream != null)
                {
                    using (StreamReader sr = new StreamReader(exResponse.GetResponseStream()))
                    {
                        string str = sr.ReadToEnd();
                        Console.WriteLine("Exception response: " + str);
                    }
                }
                return null;
            }
        }

        public static async Task<NameOutfitFactionRecord> GetCharacterName(JsonString id)
        {
            return await PlayerCache.GetPlayer(id);
        }
        public static VehicleRecord GetVehicleName(JsonString id)
        {
            if (id != null && id.InnerString != "0")
            {
                VehicleRecord vehicleRecord;
                if (VehicleCache.TryGetName(id, out vehicleRecord))
                {
                    return vehicleRecord;
                }
                return VehicleRecord.EmptyFor(id);
            }
            return new VehicleRecord() { Id = id };
        }
        public static string GetLoadoutName(JsonString id)
        {
            if (id != null && id.InnerString != "0")
            {
                JsonString jsonName;
                if (LoadoutCache.TryGetName(id, out jsonName))
                {
                    return jsonName.InnerString;
                }
                return $"<Loadout:{id.InnerString}>";
            }
            return null;
        }
        public static string GetWeaponName(JsonString id)
        {
            if (id != null && id.InnerString != "0")
            {
                JsonString jsonName;
                if (WeaponsCache.TryGetName(id, out jsonName))
                {
                    return jsonName.InnerString;
                }
                return $"<Weapon:{id.InnerString}>";
            }
            return null;
        }
    }
}
