using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Settings.Base.Global;

namespace QuickStart
{
    internal class McmSettings : AttributeGlobalSettings<McmSettings>
    {
        public override string Id => $"{SubModule.Name}_v1";
        public override string DisplayName => SubModule.DisplayName;
        public override string FolderName => SubModule.Name;
        public override string FormatType => "json2";

        [SettingPropertyBool("Skip Culture Selection Menu", HintText = SkipCultureStage_Hint, RequireRestart = false, Order = 0)]
        [SettingPropertyGroup("Settings", GroupOrder = 0)]
        public bool SkipCultureStage { get; set; } = Config.SkipCultureStage;

        [SettingPropertyBool("Skip Face/Body Customization Menu", HintText = SkipFaceGenStage_Hint, RequireRestart = false, Order = 1)]
        [SettingPropertyGroup("Settings")]
        public bool SkipFaceGenStage { get; set; } = Config.SkipFaceGenStage;

        [SettingPropertyBool("Skip Generic Backstory Menus", HintText = SkipGenericStage_Hint, RequireRestart = false, Order = 2)]
        [SettingPropertyGroup("Settings")]
        public bool SkipGenericStage { get; set; } = Config.SkipGenericStage;

        [SettingPropertyBool("Prompt for Clan Name", HintText = PromptForClanName_Hint, RequireRestart = false, Order = 3)]
        [SettingPropertyGroup("Settings")]
        public bool PromptForClanName { get; set; } = Config.PromptForClanName;

        [SettingPropertyBool("Auto-Open Banner Editor", HintText = OpenBannerEditor_Hint, RequireRestart = false, Order = 4)]
        [SettingPropertyGroup("Settings")]
        public bool OpenBannerEditor { get; set; } = Config.OpenBannerEditor;

        private const string SkipCultureStage_Hint = "Skip this menu. A random culture will be picked. [ DEFAULT: ON ]";

        private const string SkipFaceGenStage_Hint = "Skip this menu. Character will use default slider values. [ DEFAULT: ON ]";

        private const string SkipGenericStage_Hint = "Skip these menus. Random valid backstory menu options will be chosen. If"
            + " using a mod that adds more start options from which you want to select, then disable this. [ DEFAULT: ON ]";

        private const string PromptForClanName_Hint = "At start, prompt for your clan's name. Otherwise, it will default to"
            + " 'Playerclan' until/if you care to change it in the Clan Screen. [ DEFAULT: OFF ]";

        private const string OpenBannerEditor_Hint = "At start, automatically open the Banner Editor. Note that you can simply"
            + " hit the key 'B' (by default) to open it at any time. [ DEFAULT: OFF ]";
    }
}
