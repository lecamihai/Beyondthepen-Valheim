// PATCHES.CS
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
            string animalName = Localization.instance.Localize(__instance.m_name);
            CTA tameable = __instance.GetComponent<CTA>();
            
            if (tameable == null)
            {
                return true;
            }
            
            var config = AnimalConfig.GetConfig(animalName, tameable?.originalName);
            
            if (config != null)
            {
                __result = tameable.GetHoverText();
                return false;
            }
            
            return true;
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
                var addButtonMethod = typeof(ZInput).GetMethod("AddButton", BindingFlags.NonPublic | BindingFlags.Instance);
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
            NearbyAnimals.Clear();
            List<Character> animalsInRange = new List<Character>();

            Character.GetCharactersInRange(__instance.transform.position, 20f, animalsInRange);

            foreach (Character animal in animalsInRange)
            {
                var ctaComponent = animal.GetComponent<CTA>();
                
                if (animal.IsTamed() && ctaComponent != null && ctaComponent.m_TameableAI.GetFollowTarget() == __instance.gameObject)
                {
                    NearbyAnimals.Add(animal);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Player))]
    [HarmonyPatch("UpdateTeleport")]
    public static class Player_PatchUpdateTeleport
    {

        private static void Postfix(
        ref bool ___m_teleporting, 
        ref float ___m_teleportTimer, 
        ref Vector3 ___m_teleportTargetPos, 
        ref Quaternion ___m_teleportTargetRot)
    {
        if (___m_teleporting && ___m_teleportTimer > 2.0f)
        {
            foreach (Character animal in Player_PatchGetAnimals.NearbyAnimals)
            {
                if (animal == null) continue;
                Vector2 offset = UnityEngine.Random.insideUnitCircle * 2f;
                Vector3 positionOffset = new Vector3(offset.x, 0, offset.y);
                Vector3 teleportPosition = ___m_teleportTargetPos + positionOffset;
                animal.transform.position = teleportPosition;
                animal.transform.rotation = ___m_teleportTargetRot;
                animal.SetLookDir(___m_teleportTargetRot * Vector3.forward);
                Player.m_localPlayer.StartCoroutine(ConfirmTeleport(animal, ___m_teleportTargetPos, ___m_teleportTargetRot));
            }
            
            Player_PatchGetAnimals.NearbyAnimals.Clear();
        }
    }

        private static IEnumerator ConfirmTeleport(Character animal, Vector3 targetPosition, Quaternion targetRotation)
        {
            animal.transform.position = targetPosition;
            animal.transform.rotation = targetRotation;
            Rigidbody animalBody = animal.GetComponent<Rigidbody>();
            if (animalBody != null)
            {
                animalBody.velocity = Vector3.zero;
            }

            yield break;
        }
    }

}