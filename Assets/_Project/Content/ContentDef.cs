using Newtonsoft.Json;

namespace PG.Content
{
    public interface IContentDef
    {
        string Id { get; }
        int Index { get; }
    }

    // Base for every JSON content record. Index is the numeric id assigned at load time (0-based per category).
    public abstract class ContentDef : IContentDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        [JsonIgnore]
        public int Index { get; internal set; }

        // Called once after all files (base + mods) are merged.
        internal virtual void Resolve(ContentDB db, System.Collections.Generic.List<string> errors) { }
    }
}
