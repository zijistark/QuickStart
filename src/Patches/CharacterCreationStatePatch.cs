using Bannerlord.ButterLib.Common.Extensions;

using HarmonyLib;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using StoryMode.CharacterCreationSystem;

namespace QuickStart.Patches
{
    internal sealed class CharacterCreationStatePatch
    {
        private static readonly MethodInfo? TargetMethod = AccessTools.DeclaredMethod(typeof(CharacterCreationState), "NextStage");
        private static readonly MethodInfo? PatchMethod = AccessTools.DeclaredMethod(typeof(CharacterCreationStatePatch), nameof(NextStagePostfix));

        private static ILogger Log { get; set; } = default!;

        internal static bool Apply(Harmony harmony)
        {
            Log = SubModule.Instance?.GetServiceProvider()?.GetRequiredService<ILogger<CharacterCreationStatePatch>>()
                  ?? NullLogger<CharacterCreationStatePatch>.Instance;

            if (TargetMethod is null)
                Log.LogError($"{nameof(TargetMethod)} is null!");

            if (PatchMethod is null)
                Log.LogError($"{nameof(PatchMethod)} is null!");

            if (TargetMethod is null || PatchMethod is null)
                return false;

            if (harmony.Patch(TargetMethod, postfix: new HarmonyMethod(PatchMethod)) is null)
            {
                Log.LogError("Harmony failed to create patch!");
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
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
            else if (stage is CharacterCreationOptionsStage)
                SubModule.Instance.OnOptionsStage(__instance);
        }
    }
}
