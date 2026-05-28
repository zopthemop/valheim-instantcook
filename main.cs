using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace InstantCook;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class Plugin : BaseUnityPlugin
{
	public const string ModGUID = "zopthemop.instantcook";
	public const string ModName = "Instant cook";
	public const string ModVersion = "1.0.0";
	public const string ModDescription = "Cooking finishes in about a second but still burns at the regular speed";

    private void Awake()
    {
        Harmony harmony = new(ModGUID);
        harmony.PatchAll();
    }

	[HarmonyPatch(typeof(CookingStation), "UpdateCooking")]
	internal static class CookingStation_UpdateCooking_Patch
	{
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
			FieldInfo cookTimeField = AccessTools.Field(typeof(CookingStation.ItemConversion), "m_cookTime");

			int cookTimeLoadsSeen = 0;
			for (int i = 0; i < codes.Count; i++)
			{
				if (codes[i].opcode == OpCodes.Ldfld &&	Equals(codes[i].operand, cookTimeField))
				{
					cookTimeLoadsSeen++;

					// First m_cookTime load is:
					//     num > itemConversion.m_cookTime * 2f
					// (burn check)
					//
					// Second m_cookTime load is:
					//     num > itemConversion.m_cookTime
					// (cook check)
					//
					// Replace cook check with
					//     num > 1f
					if (cookTimeLoadsSeen == 2)
					{
						codes[i - 1].opcode = OpCodes.Ldc_R4;
						codes[i - 1].operand = 1f;

						codes[i].opcode = OpCodes.Nop;
						codes[i].operand = null;
					}
				}
			}

			return codes;
		}
	}

	[HarmonyPatch(typeof(CookingStation), "CookItem")]
	public static class CookingStation_CookItem_Patch
	{
		private static void Prefix(CookingStation __instance, ZNetView ___m_nview)
		{
			// If we're not the ZNetView owner we should claim ownership so our UpdateCooking is the one that runs
			if (!___m_nview.IsOwner()) {
				___m_nview.ClaimOwnership();
			}
		}

		private static void Postfix(CookingStation __instance, ZNetView ___m_nview, Transform[] ___m_slots)
		{
			for (int i = 0; i < ___m_slots.Length; i++)
			{
				float cookedTime = ___m_nview.GetZDO().GetFloat("slot" + i.ToString(), 0f);
				// This will reset cook time so everything is done at the same time
				if (cookedTime > 0f && cookedTime < 3f) {
					___m_nview.GetZDO().Set("slot" + i.ToString(), 0f);
				}
			}
		}
	}
}
