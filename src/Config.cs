namespace QuickStart
{
    internal static class Config
    {
        internal static bool ShowCultureStage { get; set; } = false;
        internal static bool ShowFaceGenStage { get; set; } = false;
        internal static bool ShowGenericStage { get; set; } = false;
        internal static bool DisableIntroVideo { get; set; } = true;
        internal static bool PromptForPlayerName { get; set; } = false;
        internal static bool PromptForClanName { get; set; } = false;
        internal static bool OpenBannerEditor { get; set; } = false;

        internal static void CopyFrom(McmSettings settings)
        {
            ShowCultureStage    = settings.ShowCultureStage;
            ShowFaceGenStage    = settings.ShowFaceGenStage;
            ShowGenericStage    = settings.ShowGenericStage;
            DisableIntroVideo   = settings.DisableIntroVideo;
            PromptForPlayerName = settings.PromptForPlayerName;
            PromptForClanName   = settings.PromptForClanName;
            OpenBannerEditor    = settings.OpenBannerEditor;
        }

        internal static string ToDebugString()
        {
            return $"{nameof(ShowCultureStage)}    = {ShowCultureStage}\n" +
                   $"{nameof(ShowFaceGenStage)}    = {ShowFaceGenStage}\n" +
                   $"{nameof(ShowGenericStage)}    = {ShowGenericStage}\n" +
                   $"{nameof(DisableIntroVideo)}   = {DisableIntroVideo}\n" +
                   $"{nameof(PromptForPlayerName)} = {PromptForPlayerName}\n" +
                   $"{nameof(PromptForClanName)}   = {PromptForClanName}\n" +
                   $"{nameof(OpenBannerEditor)}    = {OpenBannerEditor}";
        }
    }
}
