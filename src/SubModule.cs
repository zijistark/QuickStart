using Bannerlord.ButterLib.Common.Extensions;

using HarmonyLib;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.ComponentModel;
using System.Linq;

using StoryMode.Behaviors;
using StoryMode.CharacterCreationSystem;
using StoryMode.StoryModeObjects;
using StoryMode.StoryModePhases;

using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.GameState;

namespace QuickStart
{
    public sealed class SubModule : MBSubModuleBase
    {
        public static string Version => "1.0.0";

        public static string Name => typeof(SubModule).Namespace;

        public static string DisplayName => "Campaign QuickStart";

        public static string HarmonyDomain => "com.zijistark.bannerlord." + Name.ToLower();

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Instance = this;
            this.AddSerilogLoggerProvider($"{Name}.log", new[] { $"{Name}.*" });
            new Harmony(HarmonyDomain).PatchAll();
        }

        protected override void OnSubModuleUnloaded() => Log.LogInformation($"Unloaded {Name}!");

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            if (!_hasLoaded)
            {
                _hasLoaded = true;

                Log = this.GetServiceProvider().GetRequiredService<ILogger<SubModule>>();
                Log.LogInformation($"Loading {Name}...");

                if (McmSettings.Instance is { } settings)
                {
                    Log.LogInformation("MCM settings instance found.");

                    // Copy current settings to master config
                    Config.CopyFrom(settings);

                    // Register for settings property-changed events
                    settings.PropertyChanged += McmSettings_OnPropertyChanged;
                }
                else
                    Log.LogInformation("MCM settings instance NOT found. Using defaults.");

                Log.LogInformation($"Configuration:\n{Config.ToDebugString()}");
                Log.LogInformation($"Loaded {Name}!");
                InformationManager.DisplayMessage(new InformationMessage($"Loaded {DisplayName}", SignatureTextColor));
            }
        }

        private static void McmSettings_OnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (sender is McmSettings settings && args.PropertyName == McmSettings.SaveTriggered)
            {
                Config.CopyFrom(settings);
                Log.LogInformation($"MCM triggered save of settings. New configuration:\n{Config.ToDebugString()}");
            }
        }

        internal void OnCultureStage(CharacterCreationState state)
        {
            DisableElderBrother();
            TrainingFieldCampaignBehavior.SkipTutorialMission = true;

            if (Config.SkipCultureStage)
                SkipCultureStage(state);
            else
                Log.LogInformation("Culture selection stage is now under manual control.");
        }

        internal void OnFaceGenStage(CharacterCreationState state)
        {
            if (Config.SkipFaceGenStage)
                SkipFaceGenStage(state);
            else
                Log.LogInformation("Face generator stage is now under manual control.");
        }

        internal void OnGenericStage(CharacterCreationState state)
        {
            if (Config.SkipGenericStage)
                SkipGenericStage(state);
            else
                Log.LogInformation("Generic stage is now under manual control.");
        }

        internal void OnReviewStage(CharacterCreationState state) => SkipFinalStages(state);

        private void SkipCultureStage(CharacterCreationState state)
        {
            Log.LogInformation("Skipping culture selection stage...");

            if (CharacterCreationContent.Instance.Culture is null)
            {
                var culture = CharacterCreationContent.Instance.GetCultures().GetRandomElement();
                Log.LogInformation($"Auto-selected player culture: {culture.Name}");

                CharacterCreationContent.Instance.Culture = culture;
                CharacterCreationContent.CultureOnCondition(state.CharacterCreation);
                state.NextStage();
            }
        }

        private void SkipFaceGenStage(CharacterCreationState state)
        {
            Log.LogInformation("Skipping face generator stage...");
            state.NextStage();
            // MAYBE-TODO: Skipping this quickly seems to always result in the same bald man (haven't tried a woman),
            //             which is not the actual default for slider settings. It'd be nice to be able to use a
            //             potentially configured BodyProperties key.
        }

        private void SkipGenericStage(CharacterCreationState state)
        {
            Log.LogInformation("Skipping generic stage...");

            var charCreation = state.CharacterCreation;

            for (int i = 0; i < charCreation.CharacterCreationMenuCount; ++i)
            {
                var option = charCreation.GetCurrentMenuOptions(i).Where(o => o.OnCondition is null || o.OnCondition()).GetRandomElement();

                if (option is not null)
                    charCreation.RunConsequence(option, i, false);
            }

            state.NextStage();
        }

        private void SkipFinalStages(CharacterCreationState state)
        {
            if (state.CurrentStage is CharacterCreationReviewStage)
            {
                Log.LogInformation("Skipping review stage...");
                state.NextStage();
            }

            if (state.CurrentStage is CharacterCreationOptionsStage)
            {
                Log.LogInformation("Skipping campaign options stage...");
                state.NextStage();
            }

            Log.LogInformation("Skipping tutorial phase...");

            if (Campaign.Current.GetCampaignBehavior<TrainingFieldCampaignBehavior>() is { } behavior)
            {
                AccessTools.Field(typeof(TrainingFieldCampaignBehavior), "_talkedWithBrotherForTheFirstTime").SetValue(behavior, true);
                TutorialPhase.Instance.PlayerTalkedWithBrotherForTheFirstTime();
            }

            DisableElderBrother(isFirst: false); // Do it again at the end for good measure
            StoryMode.StoryMode.Current.MainStoryLine.CompleteTutorialPhase(isSkipped: true);
            ChangeClanName(null);

            if (GameStateManager.Current.ActiveState is not MapState)
            {
                Log.LogError("Completed tutorial phase, but this did not result in a MapState! Canceling further automation features.");
                return;
            }

            TeleportPlayerToSettlement();

            if (Config.PromptForClanName)
                PromptForClanName();

            if (Config.OpenBannerEditor)
                OpenBannerEditor();
        }

        private static void DisableElderBrother(bool isFirst = true)
        {
            var brother = (Hero)AccessTools.Property(typeof(StoryModeHeroes), "ElderBrother").GetValue(null);

            if (isFirst)
                PartyBase.MainParty.MemberRoster.RemoveTroop(brother.CharacterObject, 1);

            brother.ChangeState(Hero.CharacterStates.Disabled);
            brother.Clan = CampaignData.NeutralFaction;
        }

        private static void TeleportPlayerToSettlement()
        {
            foreach (var stringId in StartSettlementsToTry)
            {
                var settlement = Settlement.Find(stringId);

                if (settlement is not null)
                {
                    MobileParty.MainParty.Position2D = settlement.GatePosition;
                    ((MapState)GameStateManager.Current.ActiveState).Handler.TeleportCameraToMainParty();
                    break;
                }
            }
        }

        private static void ChangeClanName(string? name)
        {
            var txtName = new TextObject(name ?? DefaultPlayerClanName);
            Clan.PlayerClan.InitializeClan(txtName, txtName, Clan.PlayerClan.Culture, Clan.PlayerClan.Banner);
        }

        private static void PromptForClanName()
        {
            InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("{=JJiKk4ow}Select your family name: ").ToString(),
                                                                   string.Empty,
                                                                   true,
                                                                   false,
                                                                   GameTexts.FindText("str_done", null).ToString(),
                                                                   null,
                                                                   new Action<string>(ChangeClanName),
                                                                   null,
                                                                   false,
                                                                   new Func<string, bool>(IsClanNameApplicable),
                                                                   ""), false);
        }

        private static bool IsClanNameApplicable(string txt) => txt.Length <= 50 && txt.Length > 0;

        private static void OpenBannerEditor() => Game.Current.GameStateManager.PushState(Game.Current.GameStateManager.CreateState<BannerEditorState>(), 0);

        /* Non-Public Data */

        private static readonly Color SignatureTextColor = Color.FromUint(0x00F16D26);

        private const string DefaultPlayerClanName = "Playerclan";

        private static readonly string[] StartSettlementsToTry = new[]
        {
            "town_EN1", // Epicrotea
            "town_B1", // Marunath
            "town_EW2", // Zeonica
        };

        private static ILogger Log { get; set; } = default!;

        internal static SubModule Instance { get; private set; } = default!;

        private bool _hasLoaded;
    }
}