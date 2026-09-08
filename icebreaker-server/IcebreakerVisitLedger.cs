using SPTarkov.Server.Core.Models.Eft.Match;
using SysPath = System.IO.Path;

namespace Manimal.Icebreaker.Server;

// Per-profile crossing ledger. A bare "has been there" bool was enough for one gate, but
// "go there AGAIN, after quest X" has to tell trips apart, so trips are counted and the
// count reached when each gating quest was finished is stamped. A gate is then just
// Visits > Marks[questId], which cannot be satisfied by trips banked before the quest was
// taken.
//
// Lifted out of IcebreakerRaidWatchRouter, where it lived as a pile of statics, for the
// one behaviour that needed adding: Raids. The client can post /client/match/local/end
// more than once for the same raid (it re-posts on a retry, and fika delivers one per
// player), and the old code counted every post - so a single crossing could pay for a
// gate that is supposed to need two. Recording the raid id makes the count idempotent.
public sealed class IcebreakerVisitLedger(string path)
{
    private const string SlotId = "suburbs";

    private sealed class Entry
    {
        public int Visits { get; set; }
        public Dictionary<string, int> Marks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Raids { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly object _gate = new();
    private Dictionary<string, Entry>? _profiles;

    private Dictionary<string, Entry> Load()
    {
        if (_profiles != null) return _profiles;

        var loaded = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        if (System.IO.File.Exists(path))
        {
            var raw = System.IO.File.ReadAllText(path);
            // The original store was a flat list of session ids. Read it as one trip each
            // so an existing profile keeps the gate it already earned.
            if (raw.TrimStart().StartsWith("["))
            {
                foreach (var id in System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw) ?? [])
                    loaded[id] = new Entry { Visits = 1 };
            }
            else
            {
                foreach (var kv in System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Entry>>(raw) ?? [])
                    loaded[kv.Key] = kv.Value;
            }
        }
        return _profiles = loaded;
    }

    // Total crossings, ever. This is the first-visit gate.
    public bool HasVisited(string profileId)
    {
        lock (_gate) return Load().TryGetValue(profileId, out var entry) && entry.Visits > 0;
    }

    // A crossing made AFTER questId was handed in. False while the quest is unfinished
    // (no mark yet), which is what keeps the next quest in the chain hidden.
    public bool HasVisitedSince(string profileId, string questId)
    {
        lock (_gate)
            return Load().TryGetValue(profileId, out var entry)
                && entry.Marks.TryGetValue(questId, out var mark)
                && entry.Visits > mark;
    }

    // Stamps the crossing count a quest was finished at, once.
    public void MarkQuestFinished(string profileId, string questId)
    {
        lock (_gate)
        {
            var entry = Get(profileId);
            if (entry.Marks.TryAdd(questId, entry.Visits)) Save();
        }
    }

    // Returns the new crossing total, or null when this raid end is not one we count -
    // not our map, never actually started (no Results block: the client posts an end even
    // when loading aborts), or already recorded.
    public int? RecordRaidEnd(string profileId, EndLocalRaidRequestData? request)
    {
        if (request?.Results is null || string.IsNullOrEmpty(request.ServerId)) return null;

        // StartLocalRaid builds ServerId as "{location}.{side} {timestamp}", so the map
        // comes back to us on the way out - and the whole string identifies the raid.
        var dot = request.ServerId.IndexOf('.');
        if (dot < 0 || !string.Equals(request.ServerId[..dot], SlotId, StringComparison.OrdinalIgnoreCase))
            return null;

        return Record(profileId, request.ServerId);
    }

    // Any raid end on our slot counts as a visit - survive, die, MIA, whatever (user call
    // 08-19: "you can live or die, you just have to go there"). The gated quests are all
    // "you have seen the ship" beats, and a corpse on the deck has very much seen it.
    public int? Record(string profileId, string raidId)
    {
        lock (_gate)
        {
            var entry = Get(profileId);
            if (!entry.Raids.Add(raidId)) return null;   // this raid already paid
            entry.Visits++;
            Save();
            return entry.Visits;
        }
    }

    private Entry Get(string profileId)
    {
        var profiles = Load();
        if (!profiles.TryGetValue(profileId, out var entry)) profiles[profileId] = entry = new Entry();
        return entry;
    }

    // Write to a temp file and move it into place: a crash mid-write used to leave a
    // truncated store, which reads back as an empty ledger and silently revokes every
    // gate the profile had earned. Dropping the cache on failure means the next read
    // reloads from whatever is actually on disk rather than trusting memory that may no
    // longer match it.
    private void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(SysPath.GetDirectoryName(path)!);
            System.IO.File.WriteAllText(path + ".tmp", System.Text.Json.JsonSerializer.Serialize(_profiles));
            System.IO.File.Move(path + ".tmp", path, overwrite: true);
        }
        catch
        {
            _profiles = null;
            throw;
        }
    }
}
