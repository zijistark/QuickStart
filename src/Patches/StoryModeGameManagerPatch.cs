
using Bannerlord.ButterLib.Common.Extensions;

using HarmonyLib;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using StoryMode;
using StoryMode.CharacterCreationContent;

using System.Reflection;
using System.Runtime.CompilerServices;

using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace QuickStart.Patches
{
    internal sealed class StoryModeGameManagerPatch
    {
        private static readonly MethodInfo? TargetMethod = AccessTools.Method(typeof(StoryModeGameManager), "OnLoadFinished");
        private static readonly MethodInfo? PatchMethod = AccessTools.Method(typeof(StoryModeGameManagerPatch), nameof(OnLoadFinishedPrefix));

        private static ILogger Log { get; set; } = default!;

        internal static bool Apply(Harmony harmony)
        {
            Log = SubModule.Instance?.GetServiceProvider()?.GetRequiredService<ILogger<StoryModeGameManagerPatch>>()
                  ?? NullLogger<StoryModeGameManagerPatch>.Instance;

            if (TargetMethod is null)
                Log.LogError($"{nameof(TargetMethod)} is null!");

            if (PatchMethod is null)
                Log.LogError($"{nameof(PatchMethod)} is null!");

            if (TargetMethod is null || PatchMethod is null)
                return false;

            if (harmony.Patch(TargetMethod, prefix: new HarmonyMethod(PatchMethod)) is null)
            {
                Log.LogError("Harmony failed to create patch!");
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool OnLoadFinishedPrefix(StoryModeGameManager __instance)
        {
            AccessTools.DeclaredProperty(typeof(MBGameManager), "IsLoaded").SetValue(__instance, true);
            CharacterCreationState gameState = Game.Current.GameStateManager.CreateState<CharacterCreationState>(new object[]
            {
                new StoryModeCharacterCreationContent()
            });
            Game.Current.GameStateManager.CleanAndPushState(gameState, 0);
            return false;
        }
    }
}
