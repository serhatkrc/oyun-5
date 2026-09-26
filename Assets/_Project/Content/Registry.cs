using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PG.Content
{
    public sealed class Registry<T> where T : ContentDef
    {
        readonly List<T> _items = new List<T>();
        readonly Dictionary<string, int> _byKey = new Dictionary<string, int>();

        public Registry(string category)
        {
            Category = category;
        }

        public string Category { get; }
        public int Count => _items.Count;
        public IReadOnlyList<T> All => _items;
        public T this[int id] => _items[id];

        public int IdOf(string key)
        {
            if (key != null && _byKey.TryGetValue(key, out int id)) return id;
            throw new KeyNotFoundException($"{Category}: unknown id '{key}'");
        }

        public bool TryGet(string key, out T def)
        {
            if (key != null && _byKey.TryGetValue(key, out int id))
            {
                def = _items[id];
                return true;
            }
            def = null;
            return false;
        }

        public int IdOrDefault(string key, int fallback = -1) =>
            key != null && _byKey.TryGetValue(key, out int id) ? id : fallback;

        // New key -> appended with the next numeric id. Existing key (mod) -> fields are patched in place.
        internal void AddOrPatch(JObject json, JsonSerializer serializer, string source, List<string> errors)
        {
            string id = (string)json["id"];
            if (string.IsNullOrEmpty(id))
            {
                errors.Add($"{source}: record without 'id' in {Category}");
                return;
            }

            try
            {
                if (_byKey.TryGetValue(id, out int existing))
                {
                    using (var reader = json.CreateReader()) serializer.Populate(reader, _items[existing]);
                    return;
                }

                var def = json.ToObject<T>(serializer);
                def.Index = _items.Count;
                _items.Add(def);
                _byKey.Add(id, def.Index);
            }
            catch (JsonException e)
            {
                errors.Add($"{source}: {id}: {e.Message}");
            }
        }
    }
}
