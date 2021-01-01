﻿using Bannerlord.ButterLib.Common.Extensions;

using HarmonyLib;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Events;

using System;
using System.ComponentModel;
using System.Linq;

using StoryMode.Behaviors;
using StoryMode.CharacterCreationSystem;
using StoryMode.StoryModeObjects;
using StoryMode.StoryModePhases;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Actions;

namespace QuickStart
{
    public sealed class SubModule : MBSubModuleBase
    {
        public static string Version => "1.0.1";

        public static string Name => typeof(SubModule).Namespace;

        public static string DisplayName => Name;

        public static string HarmonyDomain => "com.zijistark.bannerlord." + Name.ToLower();

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Instance = this;
            this.AddSerilogLoggerProvider($"{Name}.log", new[] { $"{Name}.*" }, config => config.MinimumLevel.Is(LogEventLevel.Verbose));
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

                if (!Patches.CharacterCreationStatePatch.Apply(new Harmony(HarmonyDomain)))
                    throw new Exception($"{nameof(Patches.CharacterCreationStatePatch)} failed to apply!");

                if (McmSettings.Instance is { } settings)
                {
                    Log.LogDebug("MCM settings instance found.");

                    // Copy current settings to master config
                    Config.CopyFrom(settings);

                    // Register for settings property-changed events
                    settings.PropertyChanged += McmSettings_OnPropertyChanged;
                }
                else
                    Log.LogDebug("MCM settings instance NOT found. Using defaults.");

                Log.LogInformation($"Configuration:\n{Config.ToDebugString()}");

                if (Config.DisableIntroVideo)
                {
                    Log.LogTrace("Disabling intro video...");
                    AccessTools.DeclaredField(typeof(Module), "_splashScreenPlayed")?.SetValue(Module.CurrentModule, true);
                }

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

            if (!Config.ShowCultureStage)
                SkipCultureStage(state);
            else
                Log.LogTrace("Culture selection stage is now under manual control.");
        }

        internal void OnFaceGenStage(CharacterCreationState state)
        {
            if (!Config.ShowFaceGenStage)
                SkipFaceGenStage(state);
            else
                Log.LogTrace("Face generator stage is now under manual control.");
        }

        internal void OnGenericStage(CharacterCreationState state)
        {
            if (!Config.ShowGenericStage)
                SkipGenericStage(state);
            else
                Log.LogTrace("Generic stage is now under manual control.");
        }

        internal void OnReviewStage(CharacterCreationState state) => SkipFinalStages(state);

        internal void OnOptionsStage(CharacterCreationState state) => _ = state;

        private void SkipCultureStage(CharacterCreationState state)
        {
            Log.LogTrace("Skipping culture selection stage...");

            if (CharacterCreationContent.Instance.Culture is null)
            {
                var culture = CharacterCreationContent.Instance.GetCultures().GetRandomElement();
                Log.LogDebug($"Randomly-selected player culture: {culture.Name}");

                CharacterCreationContent.Instance.Culture = culture;
                CharacterCreationContent.CultureOnCondition(state.CharacterCreation);
                state.NextStage();
            }
        }

        private void SkipFaceGenStage(CharacterCreationState state)
        {
            Log.LogTrace("Skipping face generator stage...");
            state.NextStage();
            // MAYBE-TODO: Skipping this quickly seems to always result in the same bald man (haven't tried a woman),
            //             which is not the actual default for slider settings. It'd be nice to be able to use a
            //             potentially configured BodyProperties key.
        }

        private void SkipGenericStage(CharacterCreationState state)
        {
            Log.LogTrace("Skipping generic stage...");
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
            ChangeClanName(null);

            if (state.CurrentStage is CharacterCreationReviewStage)
            {
                Log.LogTrace("Skipping review stage...");
                state.NextStage();
            }

            if (state.CurrentStage is CharacterCreationOptionsStage)
            {
                Log.LogTrace("Skipping campaign options stage...");
                state.NextStage();
            }

            Log.LogTrace("Skipping tutorial phase...");

            if (Campaign.Current.GetCampaignBehavior<TrainingFieldCampaignBehavior>() is { } behavior)
            {
                AccessTools.Field(typeof(TrainingFieldCampaignBehavior), "_talkedWithBrotherForTheFirstTime").SetValue(behavior, true);
                TutorialPhase.Instance.PlayerTalkedWithBrotherForTheFirstTime();
            }

            DisableElderBrother(isFirst: false); // Do it again at the end for good measure
            StoryMode.StoryMode.Current.MainStoryLine.CompleteTutorialPhase(isSkipped: true);

            if (GameStateManager.Current.ActiveState is MapState)
                FinishSetup();
            else
                Log.LogCritical("Completed tutorial phase, but this did not result in a MapState! Aborting.");
        }

        private static void DisableElderBrother(bool isFirst = true)
        {
            var brother = (Hero)AccessTools.Property(typeof(StoryModeHeroes), "ElderBrother").GetValue(null);

            if (isFirst)
                PartyBase.MainParty.MemberRoster.RemoveTroop(brother.CharacterObject, 1);

            brother.ChangeState(Hero.CharacterStates.Disabled);
            brother.Clan = CampaignData.NeutralFaction;
        }

        private void FinishSetup()
        {

            if (Config.PromptForPlayerName)
                PromptForPlayerName();
            else if (Config.PromptForClanName) // Exclusive here but not upon dismissal of the player name inquiry
                PromptForClanName();

            if (Config.OpenBannerEditor)
                OpenBannerEditor();

            var kingdom = ChooseKingdom();
            var tookOverClan = Config.LandownerStart ? ChooseClanToTakeFiefsFrom(kingdom) : null;
            var playerSettlements = TakeFiefsFromClan(tookOverClan);
            FinishKingdomSetup(kingdom);
            
            var startTown = ChooseStartTown(kingdom, playerSettlements);
            TeleportPlayerToSettlement(startTown);
        }

        private static Kingdom? ChooseKingdom()
        {
            Kingdom? kingdom;

            if (!string.IsNullOrWhiteSpace(Config.KingdomId)
                && (kingdom = MBObjectManager.Instance.GetObject<Kingdom>(Config.KingdomId)) != default)
            {
                return kingdom;
            }

            // Try to choose a kingdom with a culture match (optional) that has at least 1 town (optional) & 1 clan (required)
            var kingdoms = Kingdom.All.Where(k => k.Clans.Count > 0 && !k.IsEliminated);

            kingdom = kingdoms.Where(k => k.Culture == Hero.MainHero.Culture
                                       && k.Fiefs.Where(f => f.IsTown).Any()).GetRandomElement();

            kingdom ??= kingdoms.Where(k => k.Culture == Hero.MainHero.Culture && k.Fiefs.Any()).GetRandomElement();

            kingdom ??= kingdoms.Where(k => k.Culture == Hero.MainHero.Culture).GetRandomElement();

            // Else, just assign them a random qualifying kingdom
            kingdom ??= kingdoms.GetRandomElement();
            return kingdom;
        }

        private static Clan? ChooseClanToTakeFiefsFrom(Kingdom? kingdom)
        {
            if (kingdom is null)
            {
                var clan = Clan.All.Where(c => c.Fortifications.Where(f => f.IsTown).Any()).GetRandomElement();
                clan ??= Clan.All.Where(c => c.Fortifications.Any()).GetRandomElement();

                if (clan is not null)
                    Log.LogDebug($"No kingdom: selected clan {clan.Name} of {clan.MapFaction?.Name} from which to seize fiefs.");
                else
                    Log.LogDebug($"No kingdom: failed to find clan from which to seize fiefs!");

                return clan;
            }

            if (Config.VassalStart)
            {
                var clan = kingdom.Clans.Where(c => c != kingdom.RulingClan && c.Fortifications.Where(f => f.IsTown).Any()).GetRandomElement();
                clan ??= kingdom.Clans.Where(c => c != kingdom.RulingClan && c.Fortifications.Any()).GetRandomElement();
                clan ??= kingdom.Clans.Where(c => c.Fortifications.Where(f => f.IsTown).Any()).GetRandomElement();
                clan ??= kingdom.Clans.Where(c => c.Fortifications.Any()).GetRandomElement();

                if (clan is not null)
                    Log.LogDebug($"Vassal of {kingdom.Name}: selected clan {clan.Name} from which to seize fiefs.");
                else
                    Log.LogDebug($"Vassal of {kingdom.Name}: failed to find clan from which to seize fiefs!");

                return clan;
            }

            if (Config.KingStart)
            {
                var clan = kingdom.RulingClan.Fortifications.Any() ? kingdom.RulingClan : null;
                clan ??= kingdom.Clans.Where(c => c.Fortifications.Where(f => f.IsTown).Any()).GetRandomElement();
                clan ??= kingdom.Clans.Where(c => c.Fortifications.Any()).GetRandomElement();

                if (clan is not null)
                    Log.LogDebug($"King of {kingdom.Name}: selected clan {clan.Name} (ruling clan? {kingdom.RulingClan == clan}) from which to seize fiefs.");
                else
                    Log.LogDebug($"King of {kingdom.Name}: failed to find clan from which to seize fiefs!");

                return clan;
            }

            return null;
        }

        private static List<Settlement> TakeFiefsFromClan(Clan? tookOverClan)
        {
            List<Settlement> settlementsTaken = new();

            if (tookOverClan is not null)
            {
                foreach (var s in tookOverClan.Settlements)
                {
                    ChangeOwnerOfSettlementAction.ApplyByDefault(Hero.MainHero, s);
                    settlementsTaken.Add(s);
                    Log.LogTrace($"Player clan acquired settlement: {s.Name} ({s.StringId}).");
                }

                Log.LogDebug($"Player clan acquired {settlementsTaken.Count} settlements from clan {tookOverClan.Name}.");
            }

            var home = settlementsTaken.OrderByDescending(s => s.IsTown ? 3 : s.IsCastle ? 2 : 1).FirstOrDefault();

            if (home is not null)
            {
                Log.LogTrace($"Updating player clan home settlement: {home.Name} ({home.StringId}).");
                Clan.PlayerClan.UpdateHomeSettlement(home);

                foreach (var hero in Clan.PlayerClan.Heroes)
                    hero.BornSettlement = home;
            }

            return settlementsTaken;
        }

        private static void FinishKingdomSetup(Kingdom? kingdom)
        {
            if (kingdom is null)
                return;

            if (Config.VassalStart || Config.KingStart)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, Hero.MainHero.Gold, true);
                PartyBase.MainParty.ItemRoster.Clear();
            }

            if (Config.VassalStart)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, 10_000, true);
                PartyBase.MainParty.ItemRoster.AddToCounts(DefaultItems.Grain, 2, true);
                EquipHeroFromCavalryTroop(Hero.MainHero, 3, 4);
                AddTroopsToParty(1, 12);
                AddTroopsToParty(2, 7);
                AddTroopsToParty(3, 5);

                // Swear fealty
                CharacterRelationManager.SetHeroRelation(Hero.MainHero, kingdom.Ruler, 10);
                ChangeKingdomAction.ApplyByJoinToKingdom(Clan.PlayerClan, kingdom, false);
                GainKingdomInfluenceAction.ApplyForJoiningFaction(Hero.MainHero, 50f);
            }
            else if (Config.KingStart)
            {
                ItemObject? muleItem;

                if ((muleItem = MBObjectManager.Instance.GetObject<ItemObject>("mule")) is null)
                    Log.LogDebug("Item 'mule' could not be found!");
                else
                    PartyBase.MainParty.ItemRoster.AddToCounts(muleItem, 4);

                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, 30_000, true);
                PartyBase.MainParty.ItemRoster.AddToCounts(DefaultItems.Grain, 16, true);
                EquipHeroFromCavalryTroop(Hero.MainHero, 5, 7);
                AddTroopsToParty(1, 20);
                AddTroopsToParty(2, 8);
                AddTroopsToParty(3, 8);
                AddTroopsToParty(4, 6);
                AddTroopsToParty(5, 4);
                AddTroopsToParty(6, 3);

                // Take over kingdom
                ChangeKingdomAction.ApplyByJoinToKingdom(Clan.PlayerClan, kingdom, false);
                GainKingdomInfluenceAction.ApplyForJoiningFaction(Hero.MainHero, 500f);
                kingdom.RulingClan = Clan.PlayerClan;
            }
        }

        private static void EquipHeroFromCavalryTroop(Hero hero, int minTier, int? maxTier = null)
        {
            if (maxTier is null)
                maxTier = minTier;

            // Find a high-tier cavalry-based soldier from which to template the hero's BattleEquipment,
            // preferably with the same gender (though it usually doesn't matter too much in armor, it can),
            // and of course preferably with the same culture as the hero.
            static bool IsCavalryTroop(CharacterObject c)
            {
                return c.DefaultFormationClass == FormationClass.HeavyCavalry
                    || c.DefaultFormationClass == FormationClass.Cavalry
                    || c.DefaultFormationClass == FormationClass.HorseArcher
                    || c.DefaultFormationClass == FormationClass.LightCavalry;
            }

            var troopSeq = CharacterObject.All
                .Where(c => (c.Occupation == Occupation.Soldier || c.Occupation == Occupation.Mercenary)
                         && c.Tier >= minTier
                         && c.Tier <= maxTier);

            var troop = troopSeq
                .Where(c => IsCavalryTroop(c)
                         && c.Culture == hero.Culture
                         && c.IsFemale == hero.IsFemale).GetRandomElement();

            troop ??= troopSeq.Where(c => IsCavalryTroop(c) && c.Culture == hero.Culture).GetRandomElement();
            troop ??= troopSeq.Where(c => c.Culture == hero.Culture && c.IsFemale == hero.IsFemale).GetRandomElement();
            troop ??= troopSeq.Where(c => c.Culture == hero.Culture).GetRandomElement();
            troop ??= troopSeq.Where(c => IsCavalryTroop(c) && c.IsFemale == hero.IsFemale).GetRandomElement();
            troop ??= troopSeq.Where(c => IsCavalryTroop(c)).GetRandomElement();
            troop ??= troopSeq.GetRandomElement();

            if (troop?.BattleEquipments.GetRandomElement() is { } equip)
                hero.BattleEquipment.FillFrom(equip);
            else
                Log.LogError($"Could not find cavalry-oriented equipment set for {hero.Name} "
                    + $"(minTier: {minTier}, maxTier: {maxTier}, culture: {hero.Culture.Name})!");
        }

        private static void AddTroopsToParty(int tier, int amount, PartyBase? party = null)
        {
            party ??= PartyBase.MainParty;

            var troopSeq = CharacterObject.All
                .Where(c => (c.Occupation == Occupation.Soldier || c.Occupation == Occupation.Mercenary) && c.Tier == tier);

            var troop = troopSeq.Where(c => c.Culture == party.Culture).GetRandomElement();
            troop ??= troopSeq.GetRandomElement();

            if (troop is null)
            {
                Log.LogError($"Could not find troop type to add to party (tier: {tier}, culture: {party.Culture})!");
                return;
            }

            party.AddElementToMemberRoster(troop, amount, false);
        }

        private static Settlement? ChooseStartTown(Kingdom? kingdom, List<Settlement> playerSettlements)
        {
            Settlement? town = playerSettlements.Where(s => s.IsTown).GetRandomElement();
            town ??= kingdom?.Settlements.Where(s => s.IsTown).GetRandomElement();
            town ??= Settlement.All.Where(s => s.IsTown && s.Culture == Hero.MainHero.Culture).GetRandomElement();
            town ??= Settlement.All.Where(s => s.IsTown && s.OwnerClan.Culture == Hero.MainHero.Culture).GetRandomElement();
            town ??= Settlement.All.Where(s => s.IsTown).GetRandomElement();
            return town;
        }

        private static void TeleportPlayerToSettlement(Settlement? settlement)
        {
            if (settlement is null)
            {
                Log.LogError("No town found on map. Skipping player starting position modification.");
                return;
            }

            MobileParty.MainParty.Position2D = settlement.GatePosition;
            ((MapState)GameStateManager.Current.ActiveState).Handler.TeleportCameraToMainParty();
            Log.LogTrace($"Teleported player directly to the gates of {settlement.Name} ({settlement.StringId}).");
        }

        private static void PromptForPlayerName()
        {
            InformationManager.ShowTextInquiry(new TextInquiryData(new TextObject("Select your player name: ").ToString(),
                                                                   string.Empty,
                                                                   true,
                                                                   false,
                                                                   GameTexts.FindText("str_done", null).ToString(),
                                                                   null,
                                                                   new Action<string>(ChangePlayerName),
                                                                   null,
                                                                   false,
                                                                   new Func<string, bool>(IsPlayerNameApplicable)), false);
        }

        private static bool IsPlayerNameApplicable(string txt) => txt.Length <= 24 && txt.Length > 0;

        private static void ChangePlayerName(string? name)
        {
            var txtName = new TextObject(name ?? DefaultPlayerName);
            Hero.MainHero.Name = Hero.MainHero.FirstName = txtName;
            Log.LogTrace($"Set player name: {Hero.MainHero.Name}");
            InformationManager.DisplayMessage(new InformationMessage($"Set player name to: {Hero.MainHero.Name}", SignatureTextColor));

            if (Config.PromptForClanName)
                PromptForClanName();
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
                                                                   new Func<string, bool>(IsClanNameApplicable)), false);
        }

        private static bool IsClanNameApplicable(string txt) => txt.Length <= 50 && txt.Length > 0;

        private static void ChangeClanName(string? name)
        {
            var txtName = new TextObject(name ?? DefaultPlayerClanName);
            Clan.PlayerClan.InitializeClan(txtName, txtName, Clan.PlayerClan.Culture, Clan.PlayerClan.Banner);
            Log.LogTrace($"Set player clan name: {Clan.PlayerClan.Name}");
            InformationManager.DisplayMessage(new InformationMessage($"Set player clan name to: {Clan.PlayerClan.Name}", SignatureTextColor));
        }

        private static void OpenBannerEditor()
            => Game.Current.GameStateManager.PushState(Game.Current.GameStateManager.CreateState<BannerEditorState>(), 0);

        /* Non-Public Data */

        private static readonly Color SignatureTextColor = Color.FromUint(0x00F16D26);

        private const string DefaultPlayerClanName = "Playerclan";
        private const string DefaultPlayerName = "Player";        

        private static ILogger Log { get; set; } = default!;

        internal static SubModule Instance { get; private set; } = default!;

        private bool _hasLoaded;
    }
}