using PG.Content;

namespace PG.UI
{
    // Player-facing text by localization key. Backed by ui_strings.json until Unity Localization tables (Faz 11).
    // Content names come from the content record itself (key = content id).
    public static class Loc
    {
        static ContentDB _db;

        public static string Language { get; set; } = "tr";

        public static void Init(ContentDB db) => _db = db;

        public static string T(string key) => _db != null ? _db.Text(key, Language) : key;

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        public static string Name(ContentDef def) => def?.Name ?? def?.Id ?? "";
    }
}
