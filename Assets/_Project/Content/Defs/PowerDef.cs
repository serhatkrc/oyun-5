using Newtonsoft.Json.Linq;

namespace PG.Content
{
    public sealed class PowerDef : ContentDef
    {
        public string Tab { get; set; }
        public string Type { get; set; }         // brush, drop, spawn, target, window, toggle
        public string UnlockedBy { get; set; }
        public string Description { get; set; }
        public JObject Params { get; set; }      // generator mods() output + plain keys

        public string ParamString(string key)
        {
            var t = Params?[key];
            return t != null && t.Type == JTokenType.String ? (string)t : null;
        }

        // Reads params.add[key] (numeric modifiers from the generator), falling back to params[key].
        public float ParamNumber(string key, float fallback)
        {
            var add = Params?["add"] as JObject;
            var t = add?[key] ?? Params?[key];
            if (t == null) return fallback;
            return t.Type == JTokenType.Float || t.Type == JTokenType.Integer ? (float)t : fallback;
        }

        public string[] ParamStrings(string key)
        {
            if (!(Params?[key] is JArray arr)) return System.Array.Empty<string>();
            var result = new string[arr.Count];
            for (int i = 0; i < arr.Count; i++) result[i] = (string)arr[i];
            return result;
        }
    }
}
