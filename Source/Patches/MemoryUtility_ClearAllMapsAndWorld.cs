using System.Linq;
using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Diagnostics;
using Verse.Profile;

namespace BetterPawnControl
{

    [HarmonyPatch(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld))]
    public class MemoryUtility_ClearAllMapsAndWorld_Patch
    {
        public static void Prefix()
        {
            // WorkManager.links.Clear();
            Widget_WorkTab.Reset();
        }
    }
}