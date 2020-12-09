namespace QuickStart
{
    internal static class Config
    {
        internal static bool SkipCultureStage { get; set; } = true;
        internal static bool SkipFaceGenStage { get; set; } = true;
        internal static bool SkipGenericStage { get; set; } = true;
        internal static bool PromptForClanName { get; set; } = false;
        internal static bool OpenBannerEditor { get; set; } = false;

        internal static void CopyFrom(McmSettings settings)
        {
            SkipCultureStage  = settings.SkipCultureStage;
            SkipFaceGenStage  = settings.SkipFaceGenStage;
            SkipGenericStage  = settings.SkipGenericStage;
            PromptForClanName = settings.PromptForClanName;
            OpenBannerEditor  = settings.OpenBannerEditor;
        }

        internal static string ToDebugString()
        {
            return $"{nameof(SkipCultureStage)}  = {SkipCultureStage}\n" +
                   $"{nameof(SkipFaceGenStage)}  = {SkipFaceGenStage}\n" +
                   $"{nameof(SkipGenericStage)}  = {SkipGenericStage}\n" +
                   $"{nameof(PromptForClanName)} = {PromptForClanName}\n" +
                   $"{nameof(OpenBannerEditor)}  = {OpenBannerEditor}";
        }
    }
}
