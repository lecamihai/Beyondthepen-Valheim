// animalconfig.cs //
using System.Collections.Generic;
using UnityEngine;

public class AnimalConfig
{
    public string AnimalName { get; set; }
    public float TamingTime { get; set; }
    public float FedDuration { get; set; }
    public float PregnancyDuration { get; set; }
    public int MaxCreatures { get; set; }
    public float PartnerCheckRange { get; set; }
    public float PregnancyChance { get; set; }
    public float SpawnOffset { get; set; }
    public int RequiredLovePoints { get; set; }
    public int MinOffspringLevel { get; set; }
    public List<string> FoodItems { get; set; }
    public string PetEffectPrefab { get; set; }
    public string TamedEffectPrefab { get; set; }
    public string SootheEffectPrefab { get; set; }

    public static Dictionary<string, AnimalConfig> AnimalConfigs = new Dictionary<string, AnimalConfig>
    {
        {
            "$enemy_deer", new AnimalConfig
            {
                AnimalName = "Deer",
                TamingTime = 300f,
                FedDuration = 1200f,
                PregnancyDuration = 300f,
                MaxCreatures = 6,
                PartnerCheckRange = 2f,
                PregnancyChance = 1f,
                SpawnOffset = 2f,
                RequiredLovePoints = 5,
                MinOffspringLevel = 1,
                FoodItems = new List<string> { "Blueberries", "Raspberry", "Cloudberry" },
                PetEffectPrefab = "sfx_deer_idle", // Example prefab name
                TamedEffectPrefab = "sfx_deer_idle", // Example prefab name
                SootheEffectPrefab = "vfx_creature_soothed" // Example prefab name
            }
        },
        {
            "$enemy_hare", new AnimalConfig
            {
                AnimalName = "Hare",
                TamingTime = 300f,
                FedDuration = 1200f,
                PregnancyDuration = 300f,
                MaxCreatures = 6,
                PartnerCheckRange = 2f,
                PregnancyChance = 1f,
                SpawnOffset = 2f,
                RequiredLovePoints = 5,
                MinOffspringLevel = 1,
                FoodItems = new List<string> { "Turnip", "Carrot" },
                PetEffectPrefab = "sfx_hare_idle", // Example prefab name
                TamedEffectPrefab = "sfx_hare_idle", // Example prefab name
                SootheEffectPrefab = "vfx_creature_soothed" // Example prefab name
            }
        },
        /*
        {
            "$enemy_neck", new AnimalConfig
            {
                AnimalName = "Neck",
                TamingTime = 300f,
                FedDuration = 1200f,
                PregnancyDuration = 300f,
                MaxCreatures = 6,
                PartnerCheckRange = 2f,
                PregnancyChance = 1f,
                SpawnOffset = 2f,
                RequiredLovePoints = 5,
                MinOffspringLevel = 1,
                FoodItems = new List<string> { "Blueberries", "Raspberry", "Cloudberry" },
                PetEffectPrefab = "sfx_neck_idle", // Example prefab name
                TamedEffectPrefab = "sfx_neck_idle", // Example prefab name
                SootheEffectPrefab = "sfx_neck_idle" // Example prefab name
            }
        },
        // Add more animals here...
        */
    };

    public static AnimalConfig GetConfig(string animalName, string originalName = null)
    {
        animalName = animalName.Trim().ToLower();

        // Attempt to find config with the given name
        if (AnimalConfigs.ContainsKey(animalName))
        {
            return AnimalConfigs[animalName];
        }

        // If animal has been renamed, attempt to find config with the original name
        if (originalName != null && AnimalConfigs.ContainsKey(originalName.ToLower()))
        {
            return AnimalConfigs[originalName.ToLower()];
        }

        // Fallback: attempt to strip "$enemy_" prefix for the lookup
        if (animalName.StartsWith("$enemy_"))
        {
            string simpleName = animalName.Substring(7);
            if (AnimalConfigs.ContainsKey(simpleName))
            {
                return AnimalConfigs[simpleName];
            }
        }

        return null;
    }

}