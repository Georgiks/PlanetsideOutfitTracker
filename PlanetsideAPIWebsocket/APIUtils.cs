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
        public static async Task<JsonObject> RestAPIRequest(string uriString)
        {
            Uri uri = new Uri(uriString);
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);

            WebResponse response;
            try
            {
                response = await webRequest.GetResponseAsync();
            }
            catch (WebException e)
            {
                Console.WriteLine("Request failed: " + uriString);
                // sometimes page returns error code (probably DB temp inaccessibility?)
                var exResponse = e.Response as HttpWebResponse;
                if (exResponse == null)
                {
                    Console.WriteLine("Response from server is null!");
                    Console.WriteLine("Message: " + e.Message);
                    return null;
                }
                Console.WriteLine($"Status: {(int)exResponse.StatusCode} ({exResponse.StatusCode})");
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

            Stream stream = response.GetResponseStream();
            MemoryStream ms = new MemoryStream();
            using (StreamReader reader = new StreamReader(stream))
            using (StreamWriter writer = new StreamWriter(ms))
            {
                string receivedString = await reader.ReadToEndAsync();
                //Console.WriteLine(receivedString);
                await writer.WriteAsync(receivedString);
                writer.Flush();
                ms.Position = 0;
                try
                {
                    return JsonObject.ParseFromStream(ms);
                } catch
                {
                    Console.WriteLine(receivedString);
                    throw;
                }
            }
        }

        public static async Task<NameOutfitFactionRecord> GetCharacterName(JsonString id)
        {
            return await PlayerCache.GetPlayer(id);
            /*/
            if (id != null && id.InnerString != "0")
            {
                var json = await PS2APIUtils.RestAPIRequest($@"http://census.daybreakgames.com/s:georgik/get/ps2/character_name/?character_id={id.InnerString}&c:join=character^inject_at:character^show:faction_id(outfit_member_extended^inject_at:outfit^show:alias%27name)");
                NameOutfitFactionRecord record = new NameOutfitFactionRecord();
                if ((record.Name = (json?["character_name_list"]?[0]?["name"]?["first"] as JsonString)?.InnerString) == null) record.Name = $"<Character:{id.InnerString}>";
                if ((record.Faction = (json?["character_name_list"]?[0]?["character"]?["faction_id"] as JsonString)?.InnerString) == null) record.Faction = $"<CharacterFaction:{id.InnerString}>";
                // no outfit is possible value
                record.Outfit = (json?["character_name_list"]?[0]?["character"]?["outfit"]?["alias"] as JsonString)?.InnerString;
                record.Id = id;
                return record;
            }
            return default(NameOutfitFactionRecord);
            /**/
        }
        public static async Task<VehicleRecord> GetVehicleName(JsonString id)
        {
            if (id != null && id.InnerString != "0")
            {
                //string name;
                VehicleRecord vehicleRecord;
                if (VehicleCache.TryGetName(id, out vehicleRecord))
                {
                    return vehicleRecord;
                }
                Console.WriteLine("Vehicle cache miss: " + id);

                //var json = await PS2APIUtils.RestAPIRequest($@"http://census.daybreakgames.com/s:georgik/get/ps2/vehicle/?vehicle_id={id.InnerString}&c:show=vehicle_id,name");
                //if ((name = (json?["vehicle_list"]?[0]?["name"]?["en"] as JsonString)?.InnerString) != null) return name;
                return VehicleRecord.EmptyFor(id);
            }
            return new VehicleRecord() { Id = id };
        }
        public static async Task<string> GetLoadoutName(JsonString id)
        {
            if (id != null && id.InnerString != "0")
            {
                //string name;
                JsonString jsonName;
                if (LoadoutCache.TryGetName(id, out jsonName))
                {
                    return jsonName.InnerString;
                }
                Console.WriteLine("Loadout cache miss: " + id);

                //var json = await PS2APIUtils.RestAPIRequest($@"http://census.daybreakgames.com/s:georgik/get/ps2/loadout/?loadout_id={id.InnerString}&c:show=loadout_id,profile_id&c:join=profile^on:profile_id^show:name^inject_at:profile");
                //if ((name = (json?["loadout_list"]?[0]?["profile"]?["name"]?["en"] as JsonString)?.InnerString) != null) return name;
                return $"<Loadout:{id.InnerString}>";
            }
            return null;
        }
        public static async Task<string> GetWeaponName(JsonString id)
        {
            if (id != null && id.InnerString != "0")
            {
                //string name;
                JsonString jsonName;
                if (WeaponsCache.TryGetName(id, out jsonName))
                {
                    return jsonName.InnerString;
                }
                Console.WriteLine("Weapon cache miss: " + id);

                //var json = await PS2APIUtils.RestAPIRequest($@"https://census.daybreakgames.com/s:georgik/get/ps2/item_to_weapon/?item_id={id.InnerString}&c:join=item^show:name^inject_at:item");
                //if ((name = (json?["item_to_weapon_list"]?[0]?["item"]?["name"]?["en"] as JsonString)?.InnerString) != null) return name;
                return $"<Weapon:{id.InnerString}>";
            }
            return null;
        }
    }
}
