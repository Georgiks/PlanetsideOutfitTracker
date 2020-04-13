using JsonParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanetsideAPIWebsocket
{
    public struct NameOutfitFactionRecord
    {
        public string Name;
        public string Outfit;
        public string Faction;
        public JsonString Id;
    }
    public struct VehicleRecord
    {
        public string Name;
        public JsonString Type;
        public JsonString Id;
        public static VehicleRecord EmptyFor(JsonString id)
        {
            return new VehicleRecord() { Name = $"<Vehicle:{id?.InnerString}>", Id = id, Type = new JsonString($"<Type:{id?.InnerString}>") };
        }
    }

    abstract class EventRecord
    {
        public long timestamp;

        public static string NameWithOutfit(string name, string outfit)
        {
            return (outfit == null ? "" : "[" + outfit + "]") + (name == null ? "<not found>" : name);
    }
    }

    abstract class ReviveEventRecord : EventRecord
    {
        public NameOutfitFactionRecord reviver;
        public NameOutfitFactionRecord revived;

        public static async Task<T> Parse<T>(JsonObject json) where T : ReviveEventRecord, new()
        {
            T record = new T();
            JsonString characterId = json["character_id"] as JsonString;
            JsonString otherId = json["other_id"] as JsonString;
            JsonString timestamp = json["timestamp"] as JsonString;
            var characterTask = PS2APIUtils.GetCharacterName(characterId);
            var otherTask = PS2APIUtils.GetCharacterName(otherId);

            long ts;
            if (timestamp == null || !long.TryParse(timestamp.InnerString, out ts)) ts = 0;
            record.timestamp = ts;

            await Task.WhenAll(characterTask, otherTask);
            record.reviver = await characterTask;
            record.revived = await otherTask;
            return record;
        }
    }
    sealed class NonSquadReviveEventRecord : ReviveEventRecord
    {
        public override string ToString()
        {
            return $"{NameWithOutfit(reviver.Name, reviver.Outfit)} revived {NameWithOutfit(revived.Name, revived.Outfit)}";
        }
    }
    sealed class SquadReviveEventRecord : ReviveEventRecord
    {
        public override string ToString()
        {
            return $"{NameWithOutfit(reviver.Name,reviver.Outfit)} revived squadmate {NameWithOutfit(revived.Name, revived.Outfit)}";
        }
    }

    sealed class KillEventRecord : EventRecord
    {
        public NameOutfitFactionRecord attacker;
        public NameOutfitFactionRecord victim;
        public string weaponName;
        public VehicleRecord attackerVehicle;
        public bool headshot;
        public string attackerLoadoutName;
        public string victimLoadoutName;

        public static async Task<KillEventRecord> Parse(JsonObject json)
        {
            KillEventRecord record = new KillEventRecord();
            JsonString characterId = json["character_id"] as JsonString;
            JsonString attackerId = json["attacker_character_id"] as JsonString;
            JsonString attackerVehicleId = json["attacker_vehicle_id"] as JsonString;
            JsonString attackerLoadoutId = json["attacker_loadout_id"] as JsonString;
            JsonString victimLoadoutId = json["character_loadout_id"] as JsonString;
            JsonString attackerWeaponId = json["attacker_weapon_id"] as JsonString;
            JsonString headshot = json["is_headshot"] as JsonString;
            JsonString timestamp = json["timestamp"] as JsonString;
            var characterTask = PS2APIUtils.GetCharacterName(characterId);
            var attackerTask = PS2APIUtils.GetCharacterName(attackerId);
            var attackerVehicleTask = PS2APIUtils.GetVehicleName(attackerVehicleId);
            var attackerLoadoutTask = PS2APIUtils.GetLoadoutName(attackerLoadoutId);
            var victimLoadoutTask = PS2APIUtils.GetLoadoutName(victimLoadoutId);
            var attackerWeaponTask = PS2APIUtils.GetWeaponName(attackerWeaponId);

            long ts;
            if (timestamp == null || !long.TryParse(timestamp.InnerString, out ts)) ts = 0;
            record.timestamp = ts;

            await Task.WhenAll(characterTask, attackerTask, attackerVehicleTask, attackerLoadoutTask, victimLoadoutTask, attackerWeaponTask);
            record.victim = await characterTask;
            record.attacker = await attackerTask;
            record.attackerVehicle = await attackerVehicleTask;
            record.attackerLoadoutName = await attackerLoadoutTask;
            record.victimLoadoutName = await victimLoadoutTask;
            record.weaponName = await attackerWeaponTask;
            record.headshot = headshot == null ? false : headshot.InnerString != "0";
            return record;
        }
        public override string ToString()
        {
            if (attacker.Id == victim.Id && weaponName == null)
            {
                return $"{NameWithOutfit(attacker.Name, attacker.Outfit)} commited suicide";
            }
            return $"{NameWithOutfit(attacker.Name, attacker.Outfit)}{(attackerVehicle.Name == null ? "" : " in " + attackerVehicle.Name)} playing as {attackerLoadoutName} {(victim.Faction == attacker.Faction ? "team" : "")}killed {NameWithOutfit(victim.Name, victim.Outfit)} playing as {victimLoadoutName}{(weaponName == null ? "" : " with " + weaponName)}{(headshot ? " to HEAD" : "")}";
        }
    }
    sealed class VehicleDestroyedEventRecord : EventRecord
    {
        public NameOutfitFactionRecord attacker;
        public NameOutfitFactionRecord victim;
        public string weaponName;
        public VehicleRecord attackerVehicle;
        public VehicleRecord destroyedVehicle;
        public string attackerLoadoutName;
        public static async Task<VehicleDestroyedEventRecord> Parse(JsonObject json)
        {
            VehicleDestroyedEventRecord record = new VehicleDestroyedEventRecord();
            JsonString characterId = json["character_id"] as JsonString;
            JsonString attackerId = json["attacker_character_id"] as JsonString;
            JsonString attackerVehicleId = json["attacker_vehicle_id"] as JsonString;
            JsonString attackerLoadoutId = json["attacker_loadout_id"] as JsonString;
            JsonString attackerWeaponId = json["attacker_weapon_id"] as JsonString;
            JsonString vehicleId = json["vehicle_id"] as JsonString;
            JsonString timestamp = json["timestamp"] as JsonString;
            var characterTask = PS2APIUtils.GetCharacterName(characterId);
            var attackerTask = PS2APIUtils.GetCharacterName(attackerId);
            var attackerVehicleTask = PS2APIUtils.GetVehicleName(attackerVehicleId);
            var destroyedVehicleTask = PS2APIUtils.GetVehicleName(vehicleId);
            var attackerLoadoutTask = PS2APIUtils.GetLoadoutName(attackerLoadoutId);
            var attackerWeaponTask = PS2APIUtils.GetWeaponName(attackerWeaponId);

            long ts;
            if (timestamp == null || !long.TryParse(timestamp.InnerString, out ts)) ts = 0;
            record.timestamp = ts;

            await Task.WhenAll(characterTask, attackerTask, attackerVehicleTask, attackerLoadoutTask, destroyedVehicleTask, attackerWeaponTask);
            record.victim = await characterTask;
            record.attacker = await attackerTask;
            record.attackerVehicle = await attackerVehicleTask;
            record.attackerLoadoutName = await attackerLoadoutTask;
            record.weaponName = await attackerWeaponTask;
            record.destroyedVehicle = await destroyedVehicleTask;
            return record;
        }
        public override string ToString()
        {
            if (attacker.Id == victim.Id)
            {
                return $"{NameWithOutfit(attacker.Name, attacker.Outfit)} in {attackerVehicle.Name} playing as {attackerLoadoutName} destroyed his own {destroyedVehicle.Name}";
            }
            return $"{NameWithOutfit(attacker.Name, attacker.Outfit)}{(attackerVehicle.Name == null ? "" : " in " + attackerVehicle.Name)} playing as {attackerLoadoutName} destroyed {(victim.Faction == attacker.Faction ? "teammate " : "")}{NameWithOutfit(victim.Name, victim.Outfit)}'s {destroyedVehicle.Name}{(weaponName == null ? "" : " with " + weaponName)}";
        }
    }
    class PlayerLoggingEventRecord : EventRecord
    {
        public NameOutfitFactionRecord character;
        public static async Task<T> Parse<T>(JsonObject json) where T : PlayerLoggingEventRecord, new()
        {
            T record = new T();
            JsonString characterId = json["character_id"] as JsonString;
            JsonString timestamp = json["timestamp"] as JsonString;
            var characterTask = PS2APIUtils.GetCharacterName(characterId);

            long ts;
            if (timestamp == null || !long.TryParse(timestamp.InnerString, out ts)) ts = 0;
            record.timestamp = ts;

            record.character = await characterTask;
            return record;
        }
    }
    sealed class PlayerLogoutEventRecord : PlayerLoggingEventRecord
    {
        public override string ToString()
        {
            return $"{character.Name} is OFFLINE";
        }
    }
    sealed class PlayerLoginEventRecord : PlayerLoggingEventRecord
    {
        public override string ToString()
        {
            return $"{character.Name} is ONLINE";
        }
    }

    class MinorExperienceEventRecord : EventRecord
    {
        public enum ExperienceType
        {
            Assist,
            Resupply,
            SpotAssist,
            MAXRepair,
            Unknown
        }
        public NameOutfitFactionRecord character;
        public NameOutfitFactionRecord other;
        public ExperienceType type;
        public static async Task<MinorExperienceEventRecord> Parse(JsonObject json)
        {
            MinorExperienceEventRecord record = new MinorExperienceEventRecord();
            JsonString characterId = json["character_id"] as JsonString;
            JsonString otherId = json["other_id"] as JsonString;
            JsonString experienceId = json["experience_id"] as JsonString;
            JsonString timestamp = json["timestamp"] as JsonString;
            var characterTask = PS2APIUtils.GetCharacterName(characterId);
            var otherTask = PS2APIUtils.GetCharacterName(otherId);

            long ts;
            if (timestamp == null || !long.TryParse(timestamp.InnerString, out ts)) ts = 0;
            record.timestamp = ts;
            record.type = GetExperienceType(experienceId?.InnerString);

            await Task.WhenAll(characterTask, otherTask);
            record.character = await characterTask;
            record.other = await otherTask;
            return record;

        }

        public static ExperienceType GetExperienceType(string id)
        {
            if (id == null) return ExperienceType.Unknown;
            switch (id)
            {
                case PS2APIConstants.ExperienceIdResupply:
                case PS2APIConstants.ExperienceIdSquadResupply:
                    return ExperienceType.Resupply;
                case PS2APIConstants.ExperienceIdSpotKill:
                case PS2APIConstants.ExperienceIdSquadSpotKill:
                    return ExperienceType.SpotAssist;
                case PS2APIConstants.ExperienceIdMAXRepair:
                case PS2APIConstants.ExperienceIdSquadMAXRepair:
                    return ExperienceType.MAXRepair;
                case PS2APIConstants.ExperienceIdKillAssist:
                    return ExperienceType.Assist;
                default:
                    return ExperienceType.Unknown;
            }
        }

        public override string ToString()
        {
            return $"{NameWithOutfit(character.Name, character.Outfit)} gained {type} experience{(other.Name != null ? " from " + NameWithOutfit(other.Name, other.Outfit) : "")}";
        }
    }
}
