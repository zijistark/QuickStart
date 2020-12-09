using Bannerlord.ButterLib.Common.Extensions;

using HarmonyLib;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
using System;

namespace QuickStart
{
    public sealed class SubModule : MBSubModuleBase
    {
        public static string Version => "1.0.0";
        
        public static string Name => typeof(SubModule).Namespace;
        
        public static string DisplayName => "Campaign QuickStart";
        
        public static string HarmonyDomain => "com.zijistark.bannerlord." + Name.ToLower();

        private static Color SignatureTextColor => Color.FromUint(0x00F16D26);

        internal static SubModule Instance { get; set; } = default!;

        private static ILogger Log { get; set; } = default!;

        private bool _hasLoaded;

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
                    Log.LogInformation("MCM settings instance found!");

                    // Copy current settings to master config
                    Config.CopyFrom(McmSettings.Instance!);

                    // Register for settings property-changed events
                    McmSettings.Instance!.PropertyChanged += McmSettings_OnPropertyChanged;
                }
                else
                    Log.LogInformation("MCM settings instance NOT found! Using defaults.");

                Log.LogInformation($"Configuration:\n{Config.ToMultiLineString()}");
                Log.LogInformation($"Loaded {Name}!");
                InformationManager.DisplayMessage(new InformationMessage($"Loaded {DisplayName}", SignatureTextColor));
            }
        }

        private static void McmSettings_OnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (sender is McmSettings settings && args.PropertyName == McmSettings.SaveTriggered)
            {
                Config.CopyFrom(settings);
                Log.LogInformation($"MCM triggered save of settings. New configuration:\n{Config.ToMultiLineString()}");
            }
        }

        internal void OnCultureStage(CharacterCreationState state)
        {
            DisableElderBrother();
            TrainingFieldCampaignBehavior.SkipTutorialMission = true;

            if (Config.SkipCultureStage)
                SkipCultureStage(state);
        }

        internal void OnFaceGenStage(CharacterCreationState state)
        {
            if (Config.SkipFaceGenStage)
                SkipFaceGenStage(state);
        }

        internal void OnGenericStage(CharacterCreationState state)
        {
            if (Config.SkipGenericStage)
                SkipGenericStage(state);
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
            Log.LogInformation("Skipping rest of charaction creation stages...");

            if (state.CurrentStage is CharacterCreationReviewStage)
                state.NextStage();

            if (state.CurrentStage is CharacterCreationOptionsStage)
                state.NextStage();

            Log.LogInformation("Skipping tutorial phase...");

            if (Campaign.Current.GetCampaignBehavior<TrainingFieldCampaignBehavior>() is { } behavior)
            {
                AccessTools.Field(typeof(TrainingFieldCampaignBehavior), "_talkedWithBrotherForTheFirstTime").SetValue(behavior, true);
                TutorialPhase.Instance.PlayerTalkedWithBrotherForTheFirstTime();
            }

            DisableElderBrother(isFirst: false); // Doing it again at the end for good measure

            StoryMode.StoryMode.Current.MainStoryLine.CompleteTutorialPhase(isSkipped: true);

            var startSettlement = Settlement.Find("town_EW2");

            if (startSettlement is not null && GameStateManager.Current.ActiveState is MapState mapState)
            {
                MobileParty.MainParty.Position2D = startSettlement.GatePosition;
                mapState.Handler.TeleportCameraToMainParty();
            }

            if (Config.PromptForClanName)
            {
                InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("{=JJiKk4ow}Select your family name: ", null).ToString(),
                                                                       string.Empty,
                                                                       true,
                                                                       false,
                                                                       GameTexts.FindText("str_done", null).ToString(),
                                                                       null,
                                                                       new Action<string>(OnChangeClanNameDone),
                                                                       null,
                                                                       false,
                                                                       new Func<string, bool>(IsNewClanNameApplicable),
                                                                       ""), false);
            }
        }

        private static bool IsNewClanNameApplicable(string txt) => txt.Length <= 50 && txt.Length > 0;

        private static void OnChangeClanNameDone(string txt)
        {
            var name = new TextObject(txt ?? string.Empty);
            Clan.PlayerClan.InitializeClan(name, name, Clan.PlayerClan.Culture, Clan.PlayerClan.Banner);
        }

        private void DisableElderBrother(bool isFirst = true)
        {
            var brother = (Hero)AccessTools.Property(typeof(StoryModeHeroes), "ElderBrother").GetValue(null);
            
            if (isFirst)
                PartyBase.MainParty.MemberRoster.RemoveTroop(brother.CharacterObject, 1);
            
            brother.ChangeState(Hero.CharacterStates.Disabled);
            brother.Clan = CampaignData.NeutralFaction;
        }
    }
}