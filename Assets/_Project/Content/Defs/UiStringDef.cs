namespace PG.Content
{
    // Localization key -> text. Stays until the Unity Localization tables replace it (Faz 11).
    public sealed class UiStringDef : ContentDef
    {
        public string Tr { get; set; }
        public string En { get; set; }

        public string Get(string language) => language == "en" && !string.IsNullOrEmpty(En) ? En : Tr;
    }
}
