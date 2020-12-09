namespace QuickStart
{
    internal static class Config
    {
        internal static bool SkipCultureStage { get; set; } = true;
        internal static bool SkipFaceGenStage { get; set; } = true;
        internal static bool SkipGenericStage { get; set; } = true;
        internal static bool PromptForClanName { get; set; } = false;

        internal static void CopyFrom(McmSettings settings)
        {
            SkipCultureStage  = settings.SkipCultureStage;
            SkipFaceGenStage  = settings.SkipFaceGenStage;
            SkipGenericStage  = settings.SkipGenericStage;
            PromptForClanName = settings.PromptForClanName;
        }

        internal static string ToMultiLineString()
        {
            return $"{nameof(SkipCultureStage)}  = {SkipCultureStage}\n" +
                   $"{nameof(SkipFaceGenStage)}  = {SkipFaceGenStage}\n" +
                   $"{nameof(SkipGenericStage)}  = {SkipGenericStage}\n" +
                   $"{nameof(PromptForClanName)} = {PromptForClanName}";
        }
    }
}
