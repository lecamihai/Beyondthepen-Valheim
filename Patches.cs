// PATCHES.CS //
using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System.Collections;

namespace Beyondthepen
{
    [HarmonyPatch(typeof(Character), "GetHoverText")]
    public static class Character_GetHoverText_Patch
    {
        static bool Prefix(Character __instance, ref string __result)
        {
            // Retrieve the localized name of the animal using Localization.instance.Localize()
            string animalName = Localization.instance.Localize(__instance.m_name);
            CTA tameable = __instance.GetComponent<CTA>();
            
            // If there's no CTA component, handle it gracefully
            if (tameable == null)
            {
                return true;  // Skip original method
            }
            
            // Use the original name if available for config lookup
            var config = AnimalConfig.GetConfig(animalName, tameable?.originalName);
            
            // If the config exists, return the hover text provided by the CTA component
            if (config != null)
            {
                __result = tameable.GetHoverText();
                return false;  // Skip original method
            }
            
            return true;  // Let the original method run if no config exists
        }
    }

    [HarmonyPatch(typeof(ZInput), "Initialize")]
    public static class ZInput_Initialize_Patch
    {
        public static void Postfix()
        {
            var buttonsField = typeof(ZInput).GetField("m_buttons", BindingFlags.NonPublic | BindingFlags.Instance);
            var buttons = (Dictionary<string, ZInput.ButtonDef>)buttonsField.GetValue(ZInput.instance);

            if (!buttons.ContainsKey("DeerCommand"))
            {
                // Use reflection to access the private AddButton method
                var addButtonMethod = typeof(ZInput).GetMethod("AddButton", BindingFlags.NonPublic | BindingFlags.Instance);
                
                // The path for Key.G in Input System notation is "<Keyboard>/g"
                addButtonMethod.Invoke(ZInput.instance, new object[] { "DeerCommand", "<Keyboard>/g", false, true, true, 0f, 0f });
            }
        }
    }

    [HarmonyPatch(typeof(Player))]
    [HarmonyPatch("TeleportTo")]
    public static class Player_PatchGetAnimals
    {
        public static List<Character> NearbyAnimals = new List<Character>();

        public static void Prefix(Player __instance)
        {
            NearbyAnimals.Clear();  // Clear list for each teleport action
            List<Character> animalsInRange = new List<Character>();

            // Use a method to gather all characters in range of the player
            Character.GetCharactersInRange(__instance.transform.position, 20f, animalsInRange);

            foreach (Character animal in animalsInRange)
            {
                var ctaComponent = animal.GetComponent<CTA>();
                
                // Check if the animal is tamed, has a CTA component, and is currently following the player
                if (animal.IsTamed() && ctaComponent != null && ctaComponent.m_TameableAI.GetFollowTarget() == __instance.gameObject)
                {
                    NearbyAnimals.Add(animal);  // Add only animals actively following the player
                }
            }
            //Debug.Log($"[DeerTamingMod] Found {NearbyAnimals.Count} animals for teleportation.");
        }
    }

    [HarmonyPatch(typeof(Player))]
    [HarmonyPatch("UpdateTeleport")]
    public static class Player_PatchUpdateTeleport
    {
        private static void Postfix(ref bool ___m_teleporting, ref float ___m_teleportTimer, ref Vector3 ___m_teleportTargetPos, ref Quaternion ___m_teleportTargetRot)
        {
            if (___m_teleporting && ___m_teleportTimer > 2.0f)  // Check teleport completion
            {
                foreach (Character animal in Player_PatchGetAnimals.NearbyAnimals)
                {
                    if (animal == null) continue; // Skip null references

                    // Position offset to prevent overlap
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * 2f;
                    Vector3 positionOffset = new Vector3(offset.x, 0, offset.y);
                    Vector3 teleportPosition = ___m_teleportTargetPos + positionOffset;

                    // Teleport animal to the player's target position with offset
                    animal.transform.position = teleportPosition;
                    animal.transform.rotation = ___m_teleportTargetRot;
                    animal.SetLookDir(___m_teleportTargetRot * Vector3.forward);

                    // Retry logic in case initial teleportation fails
                    Player.m_localPlayer.StartCoroutine(ConfirmTeleport(animal, ___m_teleportTargetPos, ___m_teleportTargetRot));

                    // Log to confirm teleport details
                    //Debug.Log($"[DeerTamingMod] Attempted to teleport animal '{animal.m_name}' to position {teleportPosition}");
                }

                Player_PatchGetAnimals.NearbyAnimals.Clear();  // Clear list after teleporting
            }
        }

        private static IEnumerator ConfirmTeleport(Character animal, Vector3 targetPosition, Quaternion targetRotation)
        {
            // Directly set the position and rotation once
            animal.transform.position = targetPosition;
            animal.transform.rotation = targetRotation;

            // Optional: Add some logging to verify successful teleport
            //Debug.Log($"[DeerTamingMod] Teleport successful for animal '{animal.m_name}' to position {animal.transform.position}");

            // If there's an m_body component, reset the velocity to avoid physics issues
            Rigidbody animalBody = animal.GetComponent<Rigidbody>();
            if (animalBody != null)
            {
                animalBody.velocity = Vector3.zero;
            }

            yield break;  // Exit the coroutine after one successful teleport
        }
    }

}