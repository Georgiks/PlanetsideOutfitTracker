using JsonParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    /// <summary>
    /// One-time vehicle information cache
    /// </summary>
    static class VehicleCache
    {

        static Dictionary<JsonString, VehicleRecord> VehicleIdToName { get; } = new Dictionary<JsonString, VehicleRecord>();
        static VehicleCache()
        {
            Console.WriteLine("Vehicle cache loading...");
            // get all vehicles' info and save them to dictionary
            string allVehiclesRequest = $@"https://census.daybreakgames.com/s:{PS2APIConstants.ServiceId}/get/ps2/vehicle/?c:limit=100&c:show=vehicle_id,type_id,name";
            JsonObject json = PS2APIUtils.RestAPIRequest(allVehiclesRequest).GetAwaiter().GetResult();
            var vehicles = json?["vehicle_list"] as JsonArray;
            if (vehicles == null) return;
            for (int i = 0; i < vehicles.Length; i++)
            {
                JsonString vehicleId = vehicles[i]?["vehicle_id"] as JsonString;
                JsonString name = vehicles[i]?["name"]?["en"] as JsonString;
                JsonString typeId = vehicles[i]?["type_id"] as JsonString;
                if (vehicleId == null || name == null) continue;
                VehicleIdToName.Add(vehicleId, new VehicleRecord() { Name = name?.InnerString, Id = vehicleId, Type = typeId});
            }
            Console.WriteLine("Vehicle cache loaded!");
        }

        public static VehicleRecord GetName(JsonString id)
        {
            VehicleRecord name;
            if (VehicleIdToName.TryGetValue(id, out name))
            {
                return name;
            }
            return VehicleRecord.EmptyFor(id);
        }
        public static bool TryGetName(JsonString id, out VehicleRecord name)
        {
            return VehicleIdToName.TryGetValue(id, out name);
        }
    }
}
