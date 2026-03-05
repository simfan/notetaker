using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NoteManager.Models;

namespace NoteManager.Services;

public class SupabaseService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    public SupabaseService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
        var SupabaseUrl = _config.GetSection("Supabase").GetValue<string>("Url");
        var AnonKey = _config.GetSection("Supabase").GetValue<string>("AnonKey");

        _http.BaseAddress = new Uri(SupabaseUrl);
        _http.DefaultRequestHeaders.Add("apikey", AnonKey);
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {AnonKey}");
    }

    // ── Notes ────────────────────────────────────────────────

    public async Task<List<Note>> GetNotesAsync(string? search = null, string? tagId = null, string? groupId = null)
    {
        var url = "/rest/v1/notes?select=*,group:groups(*),note_tags(tag:tags(*))&order=updated_at.desc";

        if (!string.IsNullOrWhiteSpace(search))
            url += $"&or=(title.ilike.*{Uri.EscapeDataString(search)}*,body.ilike.*{Uri.EscapeDataString(search)}*)";

        if (!string.IsNullOrWhiteSpace(groupId))
            url += $"&group_id=eq.{groupId}";

        var raw = await _http.GetFromJsonAsync<List<JsonElement>>(url) ?? new();
        var notes = raw.Select(MapNote).ToList();

        if (!string.IsNullOrWhiteSpace(tagId))
            notes = notes.Where(n => n.Tags.Any(t => t.Id == tagId)).ToList();

        return notes;
    }

    public async Task<Note> UpdateNoteAsync(Note note)
    {
        var payload = new { title = note.Title, body = note.Body, group_id = note.GroupId };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Patch, $"/rest/v1/notes?id=eq.{note.Id}") { Content = content };
        req.Headers.Add("Prefer", "return=representation");
        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return note;
    }

    public async Task DeleteNoteAsync(string id)
    {
        await _http.DeleteAsync($"/rest/v1/notes?id=eq.{id}");
    }

    public async Task SetNoteTagsAsync(string noteId, List<string> tagIds)
    {
        await _http.DeleteAsync($"/rest/v1/note_tags?note_id=eq.{noteId}");
        if (tagIds.Count > 0)
        {
            var rows = tagIds.Select(tid => new { note_id = noteId, tag_id = tid });
            var content = new StringContent(JsonSerializer.Serialize(rows), Encoding.UTF8, "application/json");
            await _http.PostAsync("/rest/v1/note_tags", content);
        }
    }

    // ── Groups ───────────────────────────────────────────────

    public async Task<List<Group>> GetGroupsAsync()
        => await _http.GetFromJsonAsync<List<Group>>("/rest/v1/groups?select=*&order=name") ?? new();

    public async Task<Group> CreateGroupAsync(string name, string color)
    {
        var payload = new { name, color, user_id = "00000000-0000-0000-0000-000000000000" };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/groups") { Content = content };
        req.Headers.Add("Prefer", "return=representation");
        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var list = await res.Content.ReadFromJsonAsync<List<Group>>();
        return list!.First();
    }

    public async Task DeleteGroupAsync(string id)
        => await _http.DeleteAsync($"/rest/v1/groups?id=eq.{id}");

    // ── Tags ─────────────────────────────────────────────────

    public async Task<List<Tag>> GetTagsAsync()
        => await _http.GetFromJsonAsync<List<Tag>>("/rest/v1/tags?select=*&order=name") ?? new();

    public async Task<Tag> CreateTagAsync(string name, string color)
    {
        var payload = new { name, color, user_id = "00000000-0000-0000-0000-000000000000" };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var req = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/tags") { Content = content };
        req.Headers.Add("Prefer", "return=representation");
        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var list = await res.Content.ReadFromJsonAsync<List<Tag>>();
        return list!.First();
    }

    public async Task DeleteTagAsync(string id)
        => await _http.DeleteAsync($"/rest/v1/tags?id=eq.{id}");

    // ── Mapping ──────────────────────────────────────────────

    private static Note MapNote(JsonElement el)
    {
        var note = new Note
        {
            Id = el.GetProperty("id").GetString() ?? "",
            UserId = el.GetProperty("user_id").GetString() ?? "",
            Title = el.GetProperty("title").GetString() ?? "Untitled",
            Body = el.TryGetProperty("body", out var b) ? b.GetString() : null,
            PhotoUrl = el.TryGetProperty("photo_url", out var p) ? p.GetString() : null,
            GroupId = el.TryGetProperty("group_id", out var g) && g.ValueKind != JsonValueKind.Null ? g.GetString() : null,
            CreatedAt = el.GetProperty("created_at").GetDateTime(),
            UpdatedAt = el.GetProperty("updated_at").GetDateTime(),
        };

        if (el.TryGetProperty("group", out var grpEl) && grpEl.ValueKind == JsonValueKind.Object)
        {
            note.Group = new Group
            {
                Id = grpEl.GetProperty("id").GetString() ?? "",
                Name = grpEl.GetProperty("name").GetString() ?? "",
                Color = grpEl.GetProperty("color").GetString() ?? "#6366f1",
            };
        }

        if (el.TryGetProperty("note_tags", out var ntArr) && ntArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var nt in ntArr.EnumerateArray())
            {
                if (nt.TryGetProperty("tag", out var t) && t.ValueKind == JsonValueKind.Object)
                {
                    note.Tags.Add(new Tag
                    {
                        Id = t.GetProperty("id").GetString() ?? "",
                        Name = t.GetProperty("name").GetString() ?? "",
                        Color = t.GetProperty("color").GetString() ?? "#6366f1",
                    });
                }
            }
        }

        return note;
    }

    // ── Tag Group Rules ──────────────────────────────────────

    public async Task<List<TagGroupRule>> GetRulesAsync()
    {
        var raw = await _http.GetFromJsonAsync<List<JsonElement>>(
            "/rest/v1/tag_group_rules?select=*,tag:tags(*),group:groups(*)&order=priority.asc") ?? new();

        return raw.Select(r => new TagGroupRule
        {
            Id = r.GetProperty("id").GetString() ?? "",
            UserId = r.GetProperty("user_id").GetString() ?? "",
            TagId = r.GetProperty("tag_id").GetString() ?? "",
            GroupId = r.GetProperty("group_id").GetString() ?? "",
            Priority = r.GetProperty("priority").GetInt32(),
            Tag = r.TryGetProperty("tag", out var t) && t.ValueKind != JsonValueKind.Null
                ? JsonSerializer.Deserialize<Tag>(t.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                : null,
            Group = r.TryGetProperty("group", out var g) && g.ValueKind != JsonValueKind.Null
                ? JsonSerializer.Deserialize<Group>(g.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                : null,
        }).ToList();
    }

    public async Task CreateRuleAsync(string tagId, string groupId, int priority)
    {
        var userId = "00000000-0000-0000-0000-000000000000";
        var payload = new { user_id = userId, tag_id = tagId, group_id = groupId, priority };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var res = await _http.PostAsync("/rest/v1/tag_group_rules", content);
        res.EnsureSuccessStatusCode();
    }

    public async Task DeleteRuleAsync(string ruleId)
    {
        await _http.DeleteAsync($"/rest/v1/tag_group_rules?id=eq.{ruleId}");
    }

    public async Task UpdateRulePriorityAsync(string ruleId, int priority)
    {
        var payload = new { priority };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _http.PatchAsync($"/rest/v1/tag_group_rules?id=eq.{ruleId}", content);
    }

    /// <summary>
    /// Given a set of tag IDs on a note, returns the group ID of the first matching rule (by priority).
    /// Returns null if no rule matches.
    /// </summary>
    public async Task<string?> ApplyRulesAsync(List<string> tagIds)
    {
        if (tagIds.Count == 0) return null;
        var rules = await GetRulesAsync();
        var match = rules.FirstOrDefault(r => tagIds.Contains(r.TagId));
        return match?.GroupId;
    }
}