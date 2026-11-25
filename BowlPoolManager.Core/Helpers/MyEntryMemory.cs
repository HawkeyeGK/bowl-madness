using System.Text.Json;
using System.Text.Json.Serialization;

namespace BowlPoolManager.Core.Helpers
{
    /// <summary>The entry a browser remembers for one pool. The name is kept only to show when the entry can no longer be found.</summary>
    public sealed record RememberedEntry(string EntryId, string Name);

    /// <summary>
    /// "Find my entry": which entry this browser remembers, per pool (Release 2). Lives only in the browser's own
    /// storage as one piece of text; nothing is sent to the server. Reading is forgiving: missing or damaged text
    /// gives an empty memory, never an error.
    /// </summary>
    public sealed class MyEntryMemory
    {
        /// <summary>The browser storage key.</summary>
        public const string StorageKey = "bm.myEntries";

        private readonly Dictionary<string, RememberedEntry> _byPool = new(StringComparer.Ordinal);

        public bool IsEmpty => _byPool.Count == 0;

        public RememberedEntry? Get(string poolId) =>
            !string.IsNullOrEmpty(poolId) && _byPool.TryGetValue(poolId, out var remembered) ? remembered : null;

        /// <summary>Remembers one entry for the pool, replacing any earlier one. Blank ids are ignored.</summary>
        public void Remember(string poolId, string entryId, string name)
        {
            if (string.IsNullOrWhiteSpace(poolId) || string.IsNullOrWhiteSpace(entryId)) return;
            _byPool[poolId] = new RememberedEntry(entryId, name ?? "");
        }

        public void Forget(string poolId)
        {
            if (!string.IsNullOrEmpty(poolId)) _byPool.Remove(poolId);
        }

        public string ToJson() =>
            JsonSerializer.Serialize(new Stored
            {
                Pools = _byPool.ToDictionary(p => p.Key, p => (StoredEntry?)new StoredEntry { EntryId = p.Value.EntryId, Name = p.Value.Name })
            });

        public static MyEntryMemory Parse(string? json)
        {
            var memory = new MyEntryMemory();
            if (string.IsNullOrWhiteSpace(json)) return memory;

            try
            {
                var stored = JsonSerializer.Deserialize<Stored>(json);
                if (stored?.Pools == null) return memory;
                foreach (var (poolId, entry) in stored.Pools)
                    memory.Remember(poolId, entry?.EntryId ?? "", entry?.Name ?? "");
            }
            catch (JsonException)
            {
                // Damaged text: start again with an empty memory.
            }
            return memory;
        }

        private sealed class Stored
        {
            [JsonPropertyName("pools")]
            public Dictionary<string, StoredEntry?>? Pools { get; set; }
        }

        private sealed class StoredEntry
        {
            [JsonPropertyName("entryId")]
            public string? EntryId { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }
    }
}
