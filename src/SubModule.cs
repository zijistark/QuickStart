using Bannerlord.ButterLib.Common.Extensions;
using Bannerlord.ButterLib.Logger.Extensions;

using HarmonyLib;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Events;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using StoryMode.Behaviors;
using StoryMode.CharacterCreationContent;
using StoryMode.StoryModeObjects;
using StoryMode.StoryModePhases;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace QuickStart
{
    public sealed class SubModule : MBSubModuleBase
    {
        public static string Version => "1.2.0.0";

        public static string Name => typeof(SubModule).Namespace;

        public static string DisplayName => Name;

        public static string HarmonyDomain => "com.zijistark.bannerlord." + Name.ToLower();

        protected override void OnSubModuleLoad()
        {
            Instance = this;
            base.OnSubModuleLoad();
            this.AddSerilogLoggerProvider($"{Name}.log", new[] { $"{Name}.*" }, config => config.MinimumLevel.Is(LogEventLevel.Verbose));
        }

        protected override void OnSubModuleUnloaded() => Log.LogInformation($"Unloaded {Name}!");

        public override void OnGameEnd(Game game) => _isSandbox = default;

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            if (!_hasLoaded)
            {
                _hasLoaded = true;

                Log = this.GetServiceProvider().GetRequiredService<ILogger<SubModule>>();
                Log.LogInformation($"Loading {Name}...");

                var harmony = new Harmony(HarmonyDomain);

                if (!Patches.CharacterCreationStatePatch.Apply(harmony))
                    throw new Exception($"{nameof(Patches.CharacterCreationStatePatch)} failed to apply!");
#if BETA
                if (!Patches.StoryModeGameManagerPatch.Apply(harmony))
                    throw new Exception($"{nameof(Patches.StoryModeGameManagerPatch)} failed to apply!");

                if (!Patches.SandBoxGameManagerPatch.Apply(harmony))
                    throw new Exception($"{nameof(Patches.SandBoxGameManagerPatch)} failed to apply!");
#endif

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
            // Figure out whether this is a Sandbox or StoryMode
            if (CharacterCreationContentBase.Instance is SandboxCharacterCreationContent)
                _isSandbox = true;
            else if (CharacterCreationContentBase.Instance is StoryModeCharacterCreationContent)
                _isSandbox = false;
            else
            {
                var msg = $"QuickStart: Unsupported type of CharacterCreationContent: {CharacterCreationContentBase.Instance.GetType().FullName}";
                Log.LogCritical($"Fatal: {msg}");
                throw new Exception(msg);
            }

            if (!_isSandbox)
            {
                DisableElderBrother();
                Campaign.Current.GetCampaignBehavior<TrainingFieldCampaignBehavior>().SkipTutorialMission = true;
            }

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

            if (CharacterCreationContentBase.Instance.GetSelectedCulture() is null)
            {
                var culture = CharacterCreationContentBase.Instance.GetCultures().RandomPick();
                Log.LogDebug($"Randomly-selected player culture: {culture.Name}");

                CharacterCreationContentBase.Instance.SetSelectedCulture(culture, state.CharacterCreation);
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
                var option = charCreation.GetCurrentMenuOptions(i).Where(o => o.OnCondition is null || o.OnCondition()).RandomPick();

                if (option is not null)
                    charCreation.RunConsequence(option, i, false);
            }

            state.NextStage();
        }

        private void SkipFinalStages(CharacterCreationState state)
        {
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

            ChangePlayerName();
            ChangeClanName();
            Log.LogTrace("Skipping tutorial phase...");

            if (!_isSandbox)
            {
                if (Campaign.Current.GetCampaignBehavior<TrainingFieldCampaignBehavior>() is { } behavior)
                {
                    AccessTools.Field(typeof(TrainingFieldCampaignBehavior), "_talkedWithBrotherForTheFirstTime").SetValue(behavior, true);
                    TutorialPhase.Instance.PlayerTalkedWithBrotherForTheFirstTime();
                }

                DisableElderBrother(isFirst: false); // Do it again at the end for good measure
                StoryMode.StoryMode.Current.MainStoryLine.CompleteTutorialPhase(isSkipped: true);
            }

            if (GameStateManager.Current.ActiveState is MapState)
                FinishSetup();
            else
                Log.LogCritical("Completed tutorial phase, but this did not result in a MapState! Aborting.");
        }

        private static void DisableElderBrother(bool isFirst = true)
        {
            var brother = StoryModeHeroes.ElderBrother;

            if (brother is null)
                return;

            if (isFirst)
                PartyBase.MainParty.MemberRoster.RemoveTroop(brother.CharacterObject, 1);

            brother.ChangeState(Hero.CharacterStates.Disabled);
            brother.Clan = CampaignData.NeutralFaction;
        }

        private void FinishSetup()
        {
            var kingdom = ChooseKingdom();
            var tookOverClan = Config.LandownerStart ? ChooseClanToTakeFiefsFrom(kingdom) : null;
            TakeFiefsFromClan(tookOverClan);
            FinishKingdomSetup(kingdom);

            var startTown = ChooseStartTown(kingdom);
            TeleportPlayerToSettlement(startTown); // Must do this before the ActiveState is not a MapState (e.g., BannerEditorState)

            if (Config.PromptForPlayerName)
                PromptForPlayerName();
            else if (Config.PromptForClanName) // Exclusive here but not upon dismissal of the player name inquiry
                PromptForClanName();

            if (Config.OpenBannerEditor)
                OpenBannerEditor();
        }

        private static Kingdom? ChooseKingdom()
        {
            Kingdom? kingdom;

            if (!string.IsNullOrWhiteSpace(Config.KingdomId))
            {
                if ((kingdom = Campaign.Current.CampaignObjectManager.Find<Kingdom>(Config.KingdomId)) != default)
                    return kingdom;
                else if (Config.VassalStart || Config.KingStart)
                    Log.LogErrorAndDisplay($"Configured kingdom ID '{Config.KingdomId}' is invalid!");
            }

            // Try to choose a kingdom with a culture match (optional) that has at least 1 town (optional) & 1 clan (required)
            var kingdoms = Kingdom.All.Where(k => k.Clans.Count > 0 && !k.IsEliminated);

            kingdom = kingdoms.Where(k => k.Culture == Hero.MainHero.Culture
                                       && k.Fiefs.Where(f => f.IsTown).Any()).RandomPick();

            kingdom ??= kingdoms.Where(k => k.Culture == Hero.MainHero.Culture && k.Fiefs.Any()).RandomPick();

            kingdom ??= kingdoms.Where(k => k.Culture == Hero.MainHero.Culture).RandomPick();

            // Else, just assign them a random qualifying kingdom
            kingdom ??= kingdoms.RandomPick();
            return kingdom;
        }

        private static Clan? ChooseClanToTakeFiefsFrom(Kingdom? kingdom)
        {
            if (kingdom is null)
            {
                var clan = Clan.All.Where(c => c.Fiefs.Where(f => f.IsTown).Any()).RandomPick();
                clan ??= Clan.All.Where(c => c.Fiefs.Any()).RandomPick();

                if (clan is not null)
                    Log.LogDebug($"No kingdom: selected clan {clan.Name} of {clan.MapFaction?.Name} from which to seize fiefs.");
                else
                    Log.LogError($"No kingdom: failed to find clan from which to seize fiefs!");

                return clan;
            }

            if (Config.VassalStart)
            {
                var clan = kingdom.Clans.Where(c => c != kingdom.RulingClan && c.Fiefs.Where(f => f.IsTown).Any()).RandomPick();
                clan ??= kingdom.Clans.Where(c => c != kingdom.RulingClan && c.Fiefs.Any()).RandomPick();
                clan ??= kingdom.Clans.Where(c => c.Fiefs.Where(f => f.IsTown).Any()).RandomPick();
                clan ??= kingdom.Clans.Where(c => c.Fiefs.Any()).RandomPick();

                if (clan is not null)
                    Log.LogDebug($"Vassal of {kingdom.Name}: selected clan {clan.Name} from which to seize fiefs.");
                else
                    Log.LogError($"Vassal of {kingdom.Name}: failed to find clan from which to seize fiefs!");

                return clan;
            }

            if (Config.KingStart)
            {
                var clan = kingdom.RulingClan.Fiefs.Any() ? kingdom.RulingClan : null;
                clan ??= kingdom.Clans.Where(c => c.Fiefs.Where(f => f.IsTown).Any()).RandomPick();
                clan ??= kingdom.Clans.Where(c => c.Fiefs.Any()).RandomPick();

                if (clan is not null)
                    Log.LogDebug($"King of {kingdom.Name}: selected clan {clan.Name} (ruling clan? {kingdom.RulingClan == clan}) from which to seize fiefs.");
                else
                    Log.LogError($"King of {kingdom.Name}: failed to find clan from which to seize fiefs!");

                return clan;
            }

            return null;
        }

        private static void TakeFiefsFromClan(Clan? tookOverClan)
        {
            List<Settlement> settlementsTaken = new();

            if (tookOverClan is not null)
            {
                foreach (var s in tookOverClan.Settlements.Where(s => s.IsTown || s.IsCastle).ToList())
                {
                    ChangeOwnerOfSettlementAction.ApplyByDefault(Hero.MainHero, s);
                    settlementsTaken.Add(s);
                    Log.LogTrace($"Player acquired: {s.Name} ({s.StringId})");
                }

                Log.LogDebug($"Player clan acquired {settlementsTaken.Count} settlements from clan {tookOverClan.Name}.");
            }

            var home = settlementsTaken.OrderBy(s => s.IsTown ? 1 : 2).FirstOrDefault();

            if (home is not null)
            {
                Log.LogTrace($"Updating player clan home settlement: {home.Name} ({home.StringId}).");
                Clan.PlayerClan.UpdateHomeSettlement(home);

                foreach (var hero in Clan.PlayerClan.Heroes)
                    hero.BornSettlement = home;
            }
        }

        private static void FinishKingdomSetup(Kingdom? kingdom)
        {
            if (kingdom is null || !(Config.VassalStart || Config.KingStart))
                return;

            var (c, h, p) = (Clan.PlayerClan, Hero.MainHero, PartyBase.MainParty);

            GiveGoldAction.ApplyBetweenCharacters(h, null, h.Gold, true);
            p.ItemRoster.Clear();

            ItemObject? muleItem;

            if ((muleItem = MBObjectManager.Instance.GetObject<ItemObject>("mule")) is null)
                Log.LogDebug("Item 'mule' could not be found!");

            if (Config.VassalStart)
            {
                Log.LogTrace("Completing vassal start scenario setup...");

                GiveGoldAction.ApplyBetweenCharacters(null, h, 10_000, true);
                p.ItemRoster.AddToCounts(DefaultItems.Grain, 5);
                
                if (muleItem is not null)
                    p.ItemRoster.AddToCounts(muleItem, 1);

                EquipHeroFromCavalryTroop(h, 3, 4);
                AddTroopsToParty(1, 12, p);
                AddTroopsToParty(2, 7, p);
                AddTroopsToParty(3, 5, p);

                // Swear fealty
                CharacterRelationManager.SetHeroRelation(h, kingdom.Ruler, 10);
                ChangeKingdomAction.ApplyByJoinToKingdom(c, kingdom, false);
                GainKingdomInfluenceAction.ApplyForJoiningFaction(h, 50f);
            }
            else if (Config.KingStart)
            {
                Log.LogTrace("Completing king start scenario setup...");

                GiveGoldAction.ApplyBetweenCharacters(null, h, 30_000, true);
                p.ItemRoster.AddToCounts(DefaultItems.Grain, 20);

                if (muleItem is not null)
                    p.ItemRoster.AddToCounts(muleItem, 4);

                EquipHeroFromCavalryTroop(h, 5, 7);
                AddTroopsToParty(1, 20, p);
                AddTroopsToParty(2, 8, p);
                AddTroopsToParty(3, 8, p);
                AddTroopsToParty(4, 6, p);
                AddTroopsToParty(5, 4, p);
                AddTroopsToParty(6, 3, p);

                // Take over kingdom
                ChangeKingdomAction.ApplyByJoinToKingdom(c, kingdom, false);
                GainKingdomInfluenceAction.ApplyForJoiningFaction(h, 500f);
                kingdom.RulingClan = c;
            }
        }

        private static void EquipHeroFromCavalryTroop(Hero hero, int minTier, int maxTier = default)
        {
            if (maxTier == default)
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
                .Where(c => c.Occupation == Occupation.Soldier
                         && c.Tier >= minTier
                         && c.Tier <= maxTier);

            var troop = troopSeq
                .Where(c => IsCavalryTroop(c)
                         && c.Culture == hero.Culture
                         && c.IsFemale == hero.IsFemale).RandomPick();

            troop ??= troopSeq.Where(c => IsCavalryTroop(c) && c.Culture == hero.Culture).RandomPick();
            troop ??= troopSeq.Where(c => c.Culture == hero.Culture && c.IsFemale == hero.IsFemale).RandomPick();
            troop ??= troopSeq.Where(c => c.Culture == hero.Culture).RandomPick();
            troop ??= troopSeq.Where(c => IsCavalryTroop(c) && c.IsFemale == hero.IsFemale).RandomPick();
            troop ??= troopSeq.Where(c => IsCavalryTroop(c)).RandomPick();
            troop ??= troopSeq.RandomPick();

            if (troop?.BattleEquipments.RandomPick() is Equipment equip)
                hero.BattleEquipment.FillFrom(equip);
            else
                Log.LogError($"Could not find battle equipment for {hero.Name} (minTier: {minTier}, "
                    + $"maxTier: {maxTier}, culture: {hero.Culture.Name})!");
        }

        private static void AddTroopsToParty(int tier, int amount, PartyBase party)
        {
            if (tier == 1)
            {
                party.AddElementToMemberRoster(party.Culture.BasicTroop, amount, false);
                return;
            }

            var troopSeq = CharacterObject.All.Where(c => c.Occupation == Occupation.Soldier && c.Tier == tier);

            var troop = troopSeq.Where(c => c.Culture == party.Culture).RandomPick();
            troop ??= troopSeq.RandomPick();

            if (troop is not null)
                party.AddElementToMemberRoster(troop, amount, false);
            else
                Log.LogError($"Could not find troop type to add to party (tier: {tier}, culture: {party.Culture})!");
         }

        private static Settlement? ChooseStartTown(Kingdom? kingdom)
        {
            var town = Clan.PlayerClan.Settlements.Where(s => s.IsTown).RandomPick();
            town ??= kingdom?.Settlements.Where(s => s.IsTown).RandomPick();
            town ??= Settlement.All.Where(s => s.IsTown && s.Culture == Hero.MainHero.Culture).RandomPick();
            town ??= Settlement.All.Where(s => s.IsTown && s.OwnerClan.Culture == Hero.MainHero.Culture).RandomPick();
            town ??= Settlement.All.Where(s => s.IsTown).RandomPick();
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
                                                                   null), false);
        }

        private static void ChangePlayerName(string? name = null)
        {
            var txtName = new TextObject(name ?? Hero.MainHero.Name.ToString());
            CharacterObject.PlayerCharacter.Name = txtName;
            Hero.MainHero.SetName(txtName);
            Log.LogTrace($"Set player name: {Hero.MainHero.Name}");

            if (name is not null) // if name was deliberately set by the user...
            {
                InformationManager.DisplayMessage(new InformationMessage($"Set player name: {Hero.MainHero.Name}", SignatureTextColor));

                if (Config.PromptForClanName)
                    PromptForClanName();
            }
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
                                                                   null), false);
        }

        private static void ChangeClanName(string? name = null)
        {
            var txtName = new TextObject(name ?? DefaultPlayerClanName);
            Clan.PlayerClan.InitializeClan(txtName, txtName, Clan.PlayerClan.Culture, Clan.PlayerClan.Banner);
            Log.LogTrace($"Set player clan name: {Clan.PlayerClan.Name}");

            if (name is not null)
                InformationManager.DisplayMessage(new InformationMessage($"Set player clan name: {Clan.PlayerClan.Name}", SignatureTextColor));
        }

        private static void OpenBannerEditor()
            => Game.Current.GameStateManager.PushState(Game.Current.GameStateManager.CreateState<BannerEditorState>(), 0);

        /* Non-Public Data */

        private static readonly Color SignatureTextColor = Color.FromUint(0x00F16D26);

        private const string DefaultPlayerClanName = "Playerclan";

        private static ILogger Log { get; set; } = default!;

        internal static SubModule Instance { get; private set; } = default!;

        private bool _hasLoaded;
        private bool _isSandbox;
    }
}
