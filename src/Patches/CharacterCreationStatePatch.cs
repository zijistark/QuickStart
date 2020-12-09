using HarmonyLib;

using System;
using System.Collections.Generic;

using StoryMode.CharacterCreationSystem;

namespace QuickStart.Patches
{
    [HarmonyPatch(typeof(CharacterCreationState))]
    internal static class CharacterCreationStatePatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("NextStage")]
        private static void NextStagePostfix(CharacterCreationState __instance, int ____stageIndex, List<KeyValuePair<int, Type>> ____stages)
        {
            if (____stageIndex == ____stages.Count)
                return; // Happens when transitioning from final stage

            var stage = __instance.CurrentStage;

            if (stage is CharacterCreationCultureStage)
                SubModule.Instance.OnCultureStage(__instance);
            else if (stage is CharacterCreationFaceGeneratorStage)
                SubModule.Instance.OnFaceGenStage(__instance);
            else if (stage is CharacterCreationGenericStage)
                SubModule.Instance.OnGenericStage(__instance);
            else if (stage is CharacterCreationReviewStage)
                SubModule.Instance.OnReviewStage(__instance);
        }
    }
}
