// mainmod.cs //
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace Beyondthepen
{
    [BepInPlugin("com.L3ca.Beyondthepen", "Beyond The Pen", "1.4.0")]
    public class DeerTamingPlugin : BaseUnityPlugin
    {
        public bool TamingLogicApplied
        {
            get { return tamingLogicApplied; }
            private set { tamingLogicApplied = value; }
        }

        private void Awake()
        {
            harmony = new Harmony("com.L3ca.deertamingmod");
            harmony.PatchAll();
            //Debug.Log("[DeerTamingMod] Harmony patches applied.");

            foreach (var animalConfig in AnimalConfig.AnimalConfigs)
            {
                var animalKey = animalConfig.Key;
                var animal = animalConfig.Value;

                // Bind the configuration entries
                tamingTimes[animalKey] = Config.Bind($"{animal.AnimalName} Config", "Taming Time", animal.TamingTime, $"Taming time for {animal.AnimalName}.");
                fedDurations[animalKey] = Config.Bind($"{animal.AnimalName} Config", "Fed Duration", animal.FedDuration, $"Fed duration for {animal.AnimalName}.");
                pregnancyDurations[animalKey] = Config.Bind($"{animal.AnimalName} Config", "Pregnancy Duration", animal.PregnancyDuration, $"Pregnancy duration for {animal.AnimalName}.");
                maxCreatures[animalKey] = Config.Bind($"{animal.AnimalName} Config", "Max Creatures", animal.MaxCreatures, $"Max creatures for {animal.AnimalName}.");
                partnerCheckRanges[animalKey] = Config.Bind($"{animal.AnimalName} Config", "Partner Check Range", animal.PartnerCheckRange, $"Partner check range for {animal.AnimalName}.");
                pregnancyChances[animalKey] = Config.Bind($"{animal.AnimalName} Config", "Pregnancy Chance", animal.PregnancyChance, $"Pregnancy chance for {animal.AnimalName}.");
                spawnOffsets[animalKey] = Config.Bind($"{animal.AnimalName} Config", "Spawn Offset", animal.SpawnOffset, $"Spawn offset for {animal.AnimalName}.");
                requiredLovePoints[animalKey] = Config.Bind($"{animal.AnimalName} Config", "Required Love Points", animal.RequiredLovePoints, $"Required love points for {animal.AnimalName}.");
                minOffspringLevels[animalKey] = Config.Bind($"{animal.AnimalName} Config", "Min Offspring Level", animal.MinOffspringLevel, $"Minimum offspring level for {animal.AnimalName}.");

                // Subscribe to config changes
                tamingTimes[animalKey].SettingChanged += OnConfigChanged;
                fedDurations[animalKey].SettingChanged += OnConfigChanged;
                pregnancyDurations[animalKey].SettingChanged += OnConfigChanged;
                maxCreatures[animalKey].SettingChanged += OnConfigChanged;
                partnerCheckRanges[animalKey].SettingChanged += OnConfigChanged;
                pregnancyChances[animalKey].SettingChanged += OnConfigChanged;
                spawnOffsets[animalKey].SettingChanged += OnConfigChanged;
                requiredLovePoints[animalKey].SettingChanged += OnConfigChanged;
                minOffspringLevels[animalKey].SettingChanged += OnConfigChanged;
            }

            ApplyNewConfigValues();
        }

        private void Start()
        {
            StartCoroutine(InitializeMod());
        }

        private void OnConfigChanged(object sender, EventArgs e)
        {
            // Reapply the new configuration values
            ApplyNewConfigValues();
        }

        private void ApplyNewConfigValues()
        {
            foreach (var TameableAI in FindObjectsOfType<TameableAI>())
            {
                CTA CTA = TameableAI.GetComponent<CTA>();
                if (CTA != null)
                {
                    var config = AnimalConfig.GetConfig(CTA.m_character.m_name);
                    if (config != null)
                    {
                        string animalKey = CTA.m_character.m_name;
                        CTA.m_tamingTime = tamingTimes[animalKey].Value;
                        CTA.m_fedDuration = fedDurations[animalKey].Value;
                    }
                }

                CTP CTP = TameableAI.GetComponent<CTP>();
                if (CTP != null)
                {
                    var config = AnimalConfig.GetConfig(CTP.m_character.m_name);
                    if (config != null)
                    {
                        string animalKey = CTP.m_character.m_name;
                        CTP.m_pregnancyDuration = pregnancyDurations[animalKey].Value;
                    }
                }

                SetAnimalFood(TameableAI, TameableAI.GetComponent<Character>().m_name);
                //Debug.Log("[DeerTamingMod] Applied new taming, fed, and pregnancy durations to animals.");
            }
        }

        private void SetAnimalFood(TameableAI TameableAI, string animalName)
        {
            if (TameableAI == null || ObjectDB.instance == null)
            {
                //Debug.LogError("[DeerTamingMod] SetAnimalFood: TameableAI or ObjectDB.instance is null");
                return;
            }

            var config = AnimalConfig.GetConfig(animalName);
            if (config == null)
            {
                //Debug.LogError($"[DeerTamingMod] SetAnimalFood: No config found for animal '{animalName}'");
                return;
            }

            List<ItemDrop> foodItems = new List<ItemDrop>();
            
            foreach (string foodName in config.FoodItems)
            {
                GameObject foodPrefab = ObjectDB.instance.GetItemPrefab(foodName);
                if (foodPrefab != null)
                {
                    ItemDrop itemDrop = foodPrefab.GetComponent<ItemDrop>();
                    if (itemDrop != null)
                    {
                        foodItems.Add(itemDrop);
                        //Debug.Log($"[DeerTamingMod] SetAnimalFood: Added {foodName} to {animalName} food items");
                    }
                }
                else
                {
                    //Debug.LogError($"[DeerTamingMod] SetAnimalFood: Food prefab '{foodName}' not found!");
                }
            }

            if (foodItems.Count > 0)
            {
                TameableAI.m_consumeItems = foodItems;
                //Debug.Log($"[DeerTamingMod] SetAnimalFood: Set consume items for {animalName}");
            }
        }

        private IEnumerator InitializeMod()
        {
            while (ZNetScene.instance == null || ObjectDB.instance == null)
            {
                yield return null;
            }

            //Debug.Log("[DeerTamingMod] ZNetScene and ObjectDB initialized. Modifying animal prefabs.");
            foreach (var animalConfig in AnimalConfig.AnimalConfigs.Values)
            {
                ModifyAnimalPrefab(animalConfig.AnimalName);
            }

            StartCoroutine(CheckForAnimals());
        }

        private IEnumerator CheckForAnimals()
        {
            while (true)
            {
                Player player = Player.m_localPlayer;
                if (player != null)
                {
                    bool hasBlueberries = HasTamingItem(player);

                    List<BaseAI> animals = FindNearbyAnimals(player.transform.position, 20f);
                    foreach (BaseAI animal in animals)
                    {
                        TameableAI tameableAI = animal.GetComponent<TameableAI>();
                        if (tameableAI != null)
                        {
                            tameableAI.SetplayerHasFood(hasBlueberries);
                        }
                    }
                }
                yield return new WaitForSeconds(5f);
            }
        }

        private List<BaseAI> FindNearbyAnimals(Vector3 position, float range)
        {
            List<BaseAI> animals = new List<BaseAI>();
            foreach (BaseAI baseAI in BaseAI.BaseAIInstances)
            {
                if (Vector3.Distance(baseAI.transform.position, position) <= range)
                {
                    animals.Add(baseAI);
                }
            }
            return animals;
        }

        public void ModifyAnimalPrefab(string animalName)
        {
            GameObject animalPrefab = ZNetScene.instance.GetPrefab(animalName);
            if (animalPrefab == null)
            {
                //Debug.LogError($"[DeerTamingMod] {animalName} prefab not found!");
                return;
            }

            ZNetView nview = animalPrefab.GetComponent<ZNetView>();
            if (nview == null)
            {
                nview = animalPrefab.AddComponent<ZNetView>();
            }

            MonsterAI monsterAI = animalPrefab.GetComponent<MonsterAI>();
            if (monsterAI != null)
            {
                DestroyImmediate(monsterAI);
            }

            Tameable Tameable = animalPrefab.GetComponent<Tameable>();
            if (Tameable != null)
            {
                DestroyImmediate(Tameable);
            }

            Procreation Procreation = animalPrefab.GetComponent<Procreation>();
            if (Procreation != null)
            {
                DestroyImmediate(Procreation);
            }

            AnimalAI animalAI = animalPrefab.GetComponent<AnimalAI>();
            if (animalAI != null)
            {
                DestroyImmediate(animalAI);
            }

            TameableAI tameableAI = animalPrefab.GetComponent<TameableAI>();
            if (tameableAI == null)
            {
                tameableAI = animalPrefab.AddComponent<TameableAI>();
            }

            CTA tameable = animalPrefab.GetComponent<CTA>();
            if (tameable == null)
            {
                tameable = animalPrefab.AddComponent<CTA>();
            }

            CTP procreation = animalPrefab.GetComponent<CTP>();
            if (procreation == null)
            {
                procreation = animalPrefab.AddComponent<CTP>();
                //Debug.Log($"[DeerTamingMod] Added CTP component to {animalName} prefab.");
            }

            var config = AnimalConfig.GetConfig(animalName);
            if (config != null)
            {
                tameable.m_tamingTime = config.TamingTime;
                tameable.m_fedDuration = config.FedDuration;
                procreation.m_pregnancyDuration = config.PregnancyDuration;
            }

            //Debug.Log($"[DeerTamingMod] {animalName} prefab modification complete.");
        }

        private bool HasTamingItem(Player player)
        {
            // Check if player has blueberries in inventory
            Inventory inventory = player.GetInventory();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_shared.m_name == "$item_blueberries" && item.m_stack > 0)
                {
                    //Debug.Log("[DeerTamingMod] Taming item detected in inventory.");
                    return true;
                }
            }
            return false;
        }
        
        public void CategorizeNearbyAnimals(Vector3 playerPosition, float range, out List<BaseAI> wildAnimals, out List<BaseAI> tamedAnimals)
        {
            wildAnimals = new List<BaseAI>();
            tamedAnimals = new List<BaseAI>();

            foreach (BaseAI baseAI in BaseAI.BaseAIInstances)
            {
                if (baseAI != null)
                {
                    float distance = Vector3.Distance(playerPosition, baseAI.transform.position);
                    if (distance <= range)
                    {
                        Character character = baseAI.GetComponent<Character>();
                        if (character != null)
                        {
                            if (character.IsTamed())
                            {
                                tamedAnimals.Add(baseAI);
                            }
                            else
                            {
                                wildAnimals.Add(baseAI);
                            }
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            //Debug.Log("[DeerTamingMod] OnDestroy: Cleaning up Deer Taming Mod");
            if (harmony != null)
            {
                harmony.UnpatchSelf();
                //Debug.Log("[DeerTamingMod] OnDestroy: Harmony patches removed");
            }
            StopAllCoroutines();
        }
        
        public static DeerTamingPlugin Instance { get; private set; }
        private Harmony harmony;
        private Dictionary<string, ConfigEntry<float>> tamingTimes = new Dictionary<string, ConfigEntry<float>>();
        private Dictionary<string, ConfigEntry<float>> fedDurations = new Dictionary<string, ConfigEntry<float>>();
        private Dictionary<string, ConfigEntry<float>> pregnancyDurations = new Dictionary<string, ConfigEntry<float>>();
        private Dictionary<string, ConfigEntry<int>> maxCreatures = new Dictionary<string, ConfigEntry<int>>();
        private Dictionary<string, ConfigEntry<float>> partnerCheckRanges = new Dictionary<string, ConfigEntry<float>>();
        private Dictionary<string, ConfigEntry<float>> pregnancyChances = new Dictionary<string, ConfigEntry<float>>();
        private Dictionary<string, ConfigEntry<float>> spawnOffsets = new Dictionary<string, ConfigEntry<float>>();
        private Dictionary<string, ConfigEntry<int>> requiredLovePoints = new Dictionary<string, ConfigEntry<int>>();
        private Dictionary<string, ConfigEntry<int>> minOffspringLevels = new Dictionary<string, ConfigEntry<int>>();
        private bool tamingLogicApplied = false;
        public static List<Character> NearbyAnimals = new List<Character>();
    }
}
