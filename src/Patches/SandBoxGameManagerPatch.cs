
using Bannerlord.ButterLib.Common.Extensions;

using HarmonyLib;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SandBox;

using System.Reflection;
using System.Runtime.CompilerServices;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace QuickStart.Patches
{
    internal sealed class SandBoxGameManagerPatch
    {
        private static readonly MethodInfo? TargetMethod = AccessTools.Method(typeof(SandBoxGameManager), "OnLoadFinished");
        private static readonly MethodInfo? PatchMethod = AccessTools.Method(typeof(SandBoxGameManagerPatch), nameof(OnLoadFinishedPrefix));

        private static ILogger Log { get; set; } = default!;

        internal static bool Apply(Harmony harmony)
        {
            Log = SubModule.Instance?.GetServiceProvider()?.GetRequiredService<ILogger<SandBoxGameManagerPatch>>()
                  ?? NullLogger<SandBoxGameManagerPatch>.Instance;

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
        private static bool OnLoadFinishedPrefix(SandBoxGameManager __instance)
        {
            var isSaveGame = (bool)AccessTools.Field(typeof(SandBoxGameManager), "_loadingSavedGame").GetValue(__instance);

            if (!isSaveGame)
            {
                AccessTools.DeclaredProperty(typeof(MBGameManager), "IsLoaded").SetValue(__instance, true);
                CharacterCreationState gameState = Game.Current.GameStateManager.CreateState<CharacterCreationState>(new object[]
                {
                    new SandboxCharacterCreationContent()
                });
                Game.Current.GameStateManager.CleanAndPushState(gameState, 0);
                return false;
            }

            if (CampaignSiegeTestStatic.IsSiegeTestBuild)
                CampaignSiegeTestStatic.DisableSiegeTest();

            Game.Current.GameStateManager.OnSavedGameLoadFinished();
            Game.Current.GameStateManager.CleanAndPushState(Game.Current.GameStateManager.CreateState<MapState>(), 0);

            var mapState = Game.Current.GameStateManager.ActiveState as MapState;
            string? menuId = mapState?.GameMenuId;

            if (!string.IsNullOrEmpty(menuId))
            {
                PlayerEncounter.Current?.OnLoad();
                Campaign.Current.GameMenuManager.SetNextMenu(menuId);
            }

            PartyBase.MainParty.Visuals?.SetMapIconAsDirty();
            Campaign.Current.CampaignInformationManager.OnGameLoaded();

            foreach (var settlement in Settlement.All)
                settlement.Party.Visuals.RefreshLevelMask(settlement.Party);

            CampaignEventDispatcher.Instance.OnGameLoadFinished();
            mapState?.OnLoadingFinished();
            AccessTools.DeclaredProperty(typeof(MBGameManager), "IsLoaded").SetValue(__instance, true);
            return false;
        }
    }
}
