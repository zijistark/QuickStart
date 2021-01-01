using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Dropdown;
using MCM.Abstractions.Settings.Base.Global;

namespace QuickStart
{
    internal class McmSettings : AttributeGlobalSettings<McmSettings>
    {
        public override string Id => $"{SubModule.Name}_v1";
        public override string DisplayName => SubModule.DisplayName;
        public override string FolderName => SubModule.Name;
        public override string FormatType => "json2";

        public McmSettings()
        {
            _vassalStart = Config.VassalStart;
            _kingStart = Config.KingStart;
        }

        // CHARACTER CREATION MENUS

        [SettingPropertyBool("Show Culture Selection Menu", HintText = ShowCultureStage_Hint, RequireRestart = false, Order = 0)]
        [SettingPropertyGroup("Character Creation Menus", GroupOrder = 0)]
        public bool ShowCultureStage { get; set; } = Config.ShowCultureStage;

        [SettingPropertyBool("Show Face/Body Customization Menu", HintText = ShowFaceGenStage_Hint, RequireRestart = false, Order = 1)]
        [SettingPropertyGroup("Character Creation Menus")]
        public bool ShowFaceGenStage { get; set; } = Config.ShowFaceGenStage;

        [SettingPropertyBool("Show Generic Backstory Menus", HintText = ShowGenericStage_Hint, RequireRestart = false, Order = 2)]
        [SettingPropertyGroup("Character Creation Menus")]
        public bool ShowGenericStage { get; set; } = Config.ShowGenericStage;

        private const string ShowCultureStage_Hint = "Do not skip this menu. Otherwise, a random and valid culture option"
            + " will automatically be chosen. [ DEFAULT: OFF ]";

        private const string ShowFaceGenStage_Hint = "Do not skip this menu. Else, character will use default slider values."
            + " [ DEFAULT: OFF ]";

        private const string ShowGenericStage_Hint = "Do not skip these menus. Else, random valid backstory menu options will"
            + " be chosen. If using a mod that adds more start options from which you want to select, then enable this."
            + " [ DEFAULT: OFF ]";

        // OTHER STARTUP SETTINGS

        [SettingPropertyBool("Disable Intro Video", HintText = DisableIntroVideo_Hint, RequireRestart = false, Order = 0)]
        [SettingPropertyGroup("Other Startup Settings", GroupOrder = 1)]
        public bool DisableIntroVideo { get; set; } = Config.DisableIntroVideo;

        [SettingPropertyBool("Prompt for Player Name", HintText = PromptForPlayerName_Hint, RequireRestart = false, Order = 1)]
        [SettingPropertyGroup("Other Startup Settings")]
        public bool PromptForPlayerName { get; set; } = Config.PromptForPlayerName;

        [SettingPropertyBool("Prompt for Clan Name", HintText = PromptForClanName_Hint, RequireRestart = false, Order = 2)]
        [SettingPropertyGroup("Other Startup Settings")]
        public bool PromptForClanName { get; set; } = Config.PromptForClanName;

        [SettingPropertyBool("Auto-Open Banner Editor", HintText = OpenBannerEditor_Hint, RequireRestart = false, Order = 3)]
        [SettingPropertyGroup("Other Startup Settings")]
        public bool OpenBannerEditor { get; set; } = Config.OpenBannerEditor;

        private const string DisableIntroVideo_Hint = "Disable TaleWorlds's time-consuming intro video / logo sequence upon"
            + " game start. [ DEFAULT: ON ]";

        private const string PromptForPlayerName_Hint = "At start, prompt for your character's name. Otherwise, it will default"
            + " to whatever was randomly chosen from cultural namelists when setting up your character. [ DEFAULT: OFF ]";

        private const string PromptForClanName_Hint = "At start, prompt for your clan's name. Otherwise, it will default to"
            + " 'Playerclan' until/if you care to change it in the Clan Screen. [ DEFAULT: OFF ]";

        private const string OpenBannerEditor_Hint = "At start, automatically open the Banner Editor. Note that you can simply"
            + " hit the key 'B' (by default) to open it at any time. [ DEFAULT: OFF ]";

        // CAMPAIGN SETUP OPTIONS

        [SettingPropertyBool("Start with an Existing Clan's Fiefs", HintText = LandownerStart_Hint, RequireRestart = false, Order = 0)]
        [SettingPropertyGroup("Campaign Setup", GroupOrder = 2)]
        public bool LandownerStart { get; set; } = Config.LandownerStart;

        [SettingPropertyBool("Start as a Vassal", HintText = VassalStart_Hint, RequireRestart = false, Order = 0)]
        [SettingPropertyGroup("Campaign Setup/Kingdom Options")]
        public bool VassalStart
        {
            get => _vassalStart;
            set
            {
                if (_vassalStart != value)
                {

                    if (value && _kingStart)
                        KingStart = false;

                    _vassalStart = value;
                    OnPropertyChanged();
                }
            }
        }

        [SettingPropertyBool("Start as a King", HintText = KingStart_Hint, RequireRestart = false, Order = 1)]
        [SettingPropertyGroup("Campaign Setup/Kingdom Options")]
        public bool KingStart
        {
            get => _kingStart;
            set
            {
                if (_kingStart != value)
                {
                    if (value && _vassalStart)
                        VassalStart = false;

                    _kingStart = value;
                    OnPropertyChanged();
                }
            }
        }

        [SettingPropertyText("Kingdom ID", HintText = KingdomId_Hint, RequireRestart = false, Order = 2)]
        [SettingPropertyGroup("Campaign Setup/Kingdom Options")]
        public string KingdomId { get; set; } = Config.KingdomId;

        private const string LandownerStart_Hint = "Take the fiefs of an existing clan and start with them in your domain."
            + " An existing clan will be selected inside your chosen kingdom, if you so choose. Else, they'll be picked"
            + " according to your character's culture. [ DEFAULT: OFF ]";

        private const string VassalStart_Hint = "Start as a vassal to a kingdom. The chosen kingdom will be picked"
            + " first by explicit specification in the 'Kingdom ID' option and otherwise by culture. [ DEFAULT: OFF ]";

        private const string KingStart_Hint = "Start as the sovereign ruler of a kingdom. The chosen kingdom will be picked"
            +" first by explicit specification in the 'Kingdom ID' option and otherwise by culture. [ DEFAULT: OFF ]";

        private const string KingdomId_Hint = "If either vassal or king start options were chosen in the previous settings, then"
            + " it's possible to specify the exact kingdom/faction ID here (e.g., empire_s). Else, leave it blank and a kingdom"
            + " will be chosen according to your character's culture. Careful to use a correct faction ID. [ Default: <BLANK> ]";

        private bool _vassalStart;
        private bool _kingStart;
    }
}
