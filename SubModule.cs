using Bannerlord.ButterLib.Common.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Linq;

using StoryMode;
using StoryMode.CharacterCreationSystem;

using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using HarmonyLib;
using StoryMode.StoryModeObjects;
using StoryMode.Behaviors;
using TaleWorlds.CampaignSystem;

namespace QuickStart
{
    public class SubModule : MBSubModuleBase
    {
        public static string Version => "1.0.0";
        public static string Name => typeof(SubModule).Namespace;
        public static string DisplayName => "Campaign QuickStart";
        public static string HarmonyDomain => "com.zijistark.bannerlord." + Name.ToLower();

        private static readonly Color SignatureTextColor = Color.FromUint(0x00F16D26);

        private static ILogger Log { get; set; } = default!;

        private static Config _config = new Config();

        private bool _hasLoaded;
        private WaitStage _waitStage = WaitStage.None;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            new Harmony(HarmonyDomain).PatchAll();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            if (!_hasLoaded)
            {
                _hasLoaded = true;
                Log = this.GetServiceProvider().GetRequiredService<ILogger<SubModule>>();
                Log.LogInformation("Loaded {module}", Name);
                InformationManager.DisplayMessage(new InformationMessage($"Loaded {DisplayName}", SignatureTextColor));
            }
        }

        public override void OnNewGameCreated(Game game, object initializerObject)
        {
            if (game.GameType is not CampaignStoryMode)
                return;

            _waitStage = WaitStage.Culture;
        }

        private class Config
        {
            internal bool ShowCultureSelect { get; set; }
            internal bool ShowFaceGen { get; set; } = true;
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            if (_waitStage != WaitStage.None && GameStateManager.Current.ActiveState is CharacterCreationState state)
            {
                if (_waitStage == WaitStage.Culture)
                {
                    SkipStart();

                    if (_config.ShowCultureSelect)
                    {
                        _waitStage = WaitStage.FaceGen;
                        return;
                    }

                    SkipCultureSelect(state);

                    if (_config.ShowFaceGen)
                    {
                        _waitStage = WaitStage.Generic;
                        return;
                    }

                    SkipFaceGen(state);
                    SkipFinal(state);
                }
                else if (_waitStage == WaitStage.FaceGen && state.CurrentStage is CharacterCreationFaceGeneratorStage)
                {
                    if (_config.ShowFaceGen)
                    {
                        _waitStage = WaitStage.Generic;
                        return;
                    }

                    SkipFaceGen(state);
                    SkipFinal(state);
                }
                else if (_waitStage == WaitStage.Generic && state.CurrentStage is CharacterCreationGenericStage)
                    SkipFinal(state);
            }
        }

        private void SkipStart()
        {
            var brother = (Hero)AccessTools.PropertyGetter(typeof(StoryModeHeroes), "ElderBrother").Invoke(null, null);
            PartyBase.MainParty.MemberRoster.RemoveTroop(brother.CharacterObject, 1);
            brother.ChangeState(Hero.CharacterStates.Disabled);
            TrainingFieldCampaignBehavior.SkipTutorialMission = true;
        }

        private void SkipCultureSelect(CharacterCreationState state)
        {
            Log.LogInformation("Skipping culture selection stage...");

            if (CharacterCreationContent.Instance.Culture is null)
            {
                var culture = CharacterCreationContent.Instance.GetCultures().GetRandomElement();
                CharacterCreationContent.Instance.Culture = culture;
                CharacterCreationContent.CultureOnCondition(state.CharacterCreation);
                state.NextStage();

                Log.LogInformation($"Auto-selected player culture: {culture.Name}");
            }
        }

        private void SkipFaceGen(CharacterCreationState state)
        {
            Log.LogInformation("Skipping face generator stage...");

            if (state.CurrentStage is CharacterCreationFaceGeneratorStage)
                state.NextStage();
        }

        private void SkipFinal(CharacterCreationState state)
        {
            Log.LogTrace("Skipping rest of charaction creation stages...");

            if (state.CurrentStage is CharacterCreationGenericStage)
            {
                var charCreation = state.CharacterCreation;

                for (int i = 0; i < charCreation.CharacterCreationMenuCount; ++i)
                {
                    var option = charCreation.GetCurrentMenuOptions(i).FirstOrDefault(o => o.OnCondition is null || o.OnCondition());

                    if (option is not null)
                        charCreation.RunConsequence(option, i, false);
                }

                state.NextStage();
            }

            if (state.CurrentStage is CharacterCreationReviewStage)
                state.NextStage();

            if (state.CurrentStage is CharacterCreationOptionsStage)
                state.NextStage();

            Log.LogTrace("Skipping tutorial phase...");
            StoryMode.StoryMode.Current.MainStoryLine.CompleteTutorialPhase(isSkipped: true);
            _waitStage = WaitStage.None;
        }

        enum WaitStage
        {
            None,
            Culture,
            FaceGen,
            Generic,
        }
    }
}