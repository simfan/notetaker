using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NoteManager.Models;

namespace NoteManager.Services;

public class SupabaseService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly AuthService _auth;
    private readonly string _supabaseUrl;
    private readonly string _anonKey;
    private readonly string _jiraProxyBase;


    public SupabaseService(HttpClient http, IConfiguration config, AuthService auth)
    {
        _http = http;
        _config = config;
        _auth = auth;
        _supabaseUrl = _config.GetSection("Supabase").GetValue<string>("Url")!.TrimEnd('/');
        _anonKey = _config.GetSection("Supabase").GetValue<string>("AnonKey")!;
        _jiraProxyBase = config["JiraProxy:BaseUrl"] ?? "http://localhost:5100";
        // No BaseAddress set here — full URLs used per-request to avoid conflict with AuthService
    }

    // ── Auth helpers ─────────────────────────────────────────

    private string BearerToken => _auth.IsAuthenticated ? _auth.Session!.AccessToken : _anonKey;
    private string UserId => _auth.UserId ?? throw new InvalidOperationException("Not authenticated");

    private HttpRequestMessage Req(HttpMethod method, string path, HttpContent? content = null)
    {
        var req = new HttpRequestMessage(method, _supabaseUrl + path) { Content = content };
        req.Headers.Add("apikey", _anonKey);
        req.Headers.Add("Authorization", $"Bearer {BearerToken}");
        return req;
    }

    // ── Notes ────────────────────────────────────────────────

    public async Task<List<Note>> GetNotesAsync(string? search = null, string? tagId = null, string? groupId = null)
    {
        var url = "/rest/v1/notes?select=*,group:groups(*),note_tags(tag:tags(*))&order=updated_at.desc";

        if (!string.IsNullOrWhiteSpace(search))
            url += $"&or=(title.ilike.*{Uri.EscapeDataString(search)}*,body.ilike.*{Uri.EscapeDataString(search)}*)";

        if (!string.IsNullOrWhiteSpace(groupId))
            url += $"&group_id=eq.{groupId}";

        var res = await _http.SendAsync(Req(HttpMethod.Get, url));
        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadFromJsonAsync<List<JsonElement>>() ?? new();
        var notes = raw.Select(MapNote).ToList();

        if (!string.IsNullOrWhiteSpace(tagId))
            notes = notes.Where(n => n.Tags.Any(t => t.Id == tagId)).ToList();

        return notes;
    }

    public async Task UpdateNoteAsync(Note note)
    {
        var metadataJson = JsonSerializer.SerializeToElement(note.Metadata);
        var payload = new
        {
            title = note.Title,
            body = note.Body,
            group_id = note.GroupId,
            photo_url = note.PhotoUrl,
            note_type = note.NoteTypeRaw,
            note_metadata = metadataJson,
            updated_at = DateTime.UtcNow
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var res = await _http.SendAsync(Req(HttpMethod.Patch, $"/rest/v1/notes?id=eq.{note.Id}", content));
        res.EnsureSuccessStatusCode();
    }

    public async Task DeleteNoteAsync(string id)
    {
        await _http.SendAsync(Req(HttpMethod.Delete, $"/rest/v1/notes?id=eq.{id}"));
    }

    public async Task SetNoteTagsAsync(string noteId, List<string> tagIds)
    {
        await _http.SendAsync(Req(HttpMethod.Delete, $"/rest/v1/note_tags?note_id=eq.{noteId}"));
        if (tagIds.Count > 0)
        {
            var rows = tagIds.Select(tid => new { note_id = noteId, tag_id = tid });
            var content = new StringContent(JsonSerializer.Serialize(rows), Encoding.UTF8, "application/json");
            await _http.SendAsync(Req(HttpMethod.Post, "/rest/v1/note_tags", content));
        }
    }

    // ── Groups ───────────────────────────────────────────────

    public async Task<List<Group>> GetGroupsAsync()
    {
        var res = await _http.SendAsync(Req(HttpMethod.Get, "/rest/v1/groups?select=*&order=name"));
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<List<Group>>() ?? new();
    }

    public async Task CreateGroupAsync(string name, string color, string templateType = "standard")
    {
        var payload = new { user_id = UserId, name, color, template_type = templateType };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var res = await _http.SendAsync(Req(HttpMethod.Post, "/rest/v1/groups", content));
        res.EnsureSuccessStatusCode();
    }

    public async Task DeleteGroupAsync(string id)
    {
        await _http.SendAsync(Req(HttpMethod.Delete, $"/rest/v1/groups?id=eq.{id}"));
    }

    // ── Tags ─────────────────────────────────────────────────

    public async Task<List<Tag>> GetTagsAsync()
    {
        var res = await _http.SendAsync(Req(HttpMethod.Get, "/rest/v1/tags?select=*&order=name"));
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<List<Tag>>() ?? new();
    }

    public async Task<Tag> CreateTagAsync(string name, string color)
    {
        var payload = new { name, color, user_id = UserId };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var req = Req(HttpMethod.Post, "/rest/v1/tags", content);
        req.Headers.Add("Prefer", "return=representation");
        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var list = await res.Content.ReadFromJsonAsync<List<Tag>>();
        return list!.First();
    }

    public async Task DeleteTagAsync(string id)
    {
        await _http.SendAsync(Req(HttpMethod.Delete, $"/rest/v1/tags?id=eq.{id}"));
    }

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
            NoteTypeRaw = el.TryGetProperty("note_type", out var nt) && nt.ValueKind != JsonValueKind.Null
                            ? nt.GetString() ?? "note"
                            : "note",
            CreatedAt = el.GetProperty("created_at").GetDateTime(),
            UpdatedAt = el.GetProperty("updated_at").GetDateTime(),
        };

        if (el.TryGetProperty("note_metadata", out var metaEl) && metaEl.ValueKind == JsonValueKind.Object)
            note.NoteMetadataRaw = metaEl;

        if (el.TryGetProperty("group", out var grpEl) && grpEl.ValueKind == JsonValueKind.Object)
        {
            note.Group = new Group
            {
                Id = grpEl.GetProperty("id").GetString() ?? "",
                Name = grpEl.GetProperty("name").GetString() ?? "",
                Color = grpEl.GetProperty("color").GetString() ?? "#6366f1",
                TemplateTypeRaw = grpEl.TryGetProperty("template_type", out var tt) && tt.ValueKind != JsonValueKind.Null
                                    ? tt.GetString() ?? "standard"
                                    : "standard",
            };
        }

        if (el.TryGetProperty("note_tags", out var ntArr) && ntArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var nt2 in ntArr.EnumerateArray())
            {
                if (nt2.TryGetProperty("tag", out var t) && t.ValueKind == JsonValueKind.Object)
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
        var res = await _http.SendAsync(Req(HttpMethod.Get,
            "/rest/v1/tag_group_rules?select=*,tag:tags(*),group:groups(*)&order=priority.asc"));
        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadFromJsonAsync<List<JsonElement>>() ?? new();

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
        var payload = new { user_id = UserId, tag_id = tagId, group_id = groupId, priority };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var res = await _http.SendAsync(Req(HttpMethod.Post, "/rest/v1/tag_group_rules", content));
        res.EnsureSuccessStatusCode();
    }

    public async Task DeleteRuleAsync(string ruleId)
    {
        await _http.SendAsync(Req(HttpMethod.Delete, $"/rest/v1/tag_group_rules?id=eq.{ruleId}"));
    }

    public async Task UpdateRulePriorityAsync(string ruleId, int priority)
    {
        var payload = new { priority };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _http.SendAsync(Req(HttpMethod.Patch, $"/rest/v1/tag_group_rules?id=eq.{ruleId}", content));
    }

    public async Task<string?> ApplyRulesAsync(List<string> tagIds)
    {
        if (tagIds.Count == 0) return null;
        var rules = await GetRulesAsync();
        var match = rules.FirstOrDefault(r => tagIds.Contains(r.TagId));
        return match?.GroupId;
    }

    public async Task UpdateNoteMetadataAsync(string noteId, NoteMetadata metadata)
    {
        var payload = new
        {
            note_metadata = JsonSerializer.SerializeToElement(metadata),
            updated_at = DateTime.UtcNow
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var res = await _http.SendAsync(Req(HttpMethod.Patch, $"/rest/v1/notes?id=eq.{noteId}", content));
        res.EnsureSuccessStatusCode();
    }

    public async Task UpdateNoteTypeAsync(string noteId, string noteType)
    {
        var payload = new { note_type = noteType, updated_at = DateTime.UtcNow };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var res = await _http.SendAsync(Req(HttpMethod.Patch, $"/rest/v1/notes?id=eq.{noteId}", content));
        res.EnsureSuccessStatusCode();
    }

    // ── Projects ─────────────────────────────────────────────

    public async Task<List<Project>> GetProjectsAsync()
    {
        var res = await _http.SendAsync(Req(HttpMethod.Get,
            "/rest/v1/projects?select=*,tag:tags(*)&order=name.asc"));
        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadFromJsonAsync<List<JsonElement>>() ?? new();

        return raw.Select(r => new Project
        {
            Id = r.GetProperty("id").GetString() ?? "",
            UserId = r.GetProperty("user_id").GetString() ?? "",
            Name = r.GetProperty("name").GetString() ?? "",
            TagId = r.TryGetProperty("tag_id", out var tid) && tid.ValueKind != JsonValueKind.Null
                        ? tid.GetString() : null,
            CreatedAt = r.GetProperty("created_at").GetDateTime(),
            Tag = r.TryGetProperty("tag", out var t) && t.ValueKind != JsonValueKind.Null
                        ? JsonSerializer.Deserialize<Tag>(t.GetRawText(),
                              new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        : null
        }).ToList();
    }

    public async Task CreateProjectAsync(string name, string? tagId)
    {
        var payload = new { user_id = UserId, name, tag_id = tagId };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var res = await _http.SendAsync(Req(HttpMethod.Post, "/rest/v1/projects", content));
        res.EnsureSuccessStatusCode();
    }

    public async Task DeleteProjectAsync(string projectId)
    {
        await _http.SendAsync(Req(HttpMethod.Delete, $"/rest/v1/projects?id=eq.{projectId}"));
    }

    public async Task<Note?> GetNoteByIdAsync(string id)
    {
        var url = $"/rest/v1/notes?select=*,group:groups(*),note_tags(tag:tags(*))&id=eq.{id}&limit=1";
        var res = await _http.SendAsync(Req(HttpMethod.Get, url));
        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadFromJsonAsync<List<JsonElement>>() ?? new();
        return raw.Count > 0 ? MapNote(raw[0]) : null;
    }

    // ── Public Sharing ───────────────────────────────────────

    public string PublicEndpointBase => _supabaseUrl + "/rest/v1/rpc/get_public_group_notes";

    public async Task SetGroupPublicAsync(string groupId, bool isPublic)
    {
        var payload = new { is_public = isPublic };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var req = Req(HttpMethod.Patch, $"/rest/v1/groups?id=eq.{groupId}", content);
        req.Headers.Add("Prefer", "return=minimal");
        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
    }
    // ── Jira ─────────────────────────────────────────────────────────────────────

    public async Task<bool> GetJiraStatusAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_jiraProxyBase}/jira/status");
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json).RootElement;
            return doc.GetProperty("connected").GetBoolean();
        }
        catch { return false; }
    }

    public async Task<List<JiraProject>> GetJiraProjectsAsync()
    {
        var response = await _http.GetAsync($"{_jiraProxyBase}/jira/projects");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;

        var results = new List<JiraProject>();
        if (doc.TryGetProperty("values", out var values))
        {
            foreach (var p in values.EnumerateArray())
            {
                results.Add(new JiraProject
                {
                    Id = p.GetProperty("id").GetString() ?? "",
                    Key = p.GetProperty("key").GetString() ?? "",
                    Name = p.GetProperty("name").GetString() ?? ""
                });
            }
        }
        return results;
    }

    public async Task<List<JiraIssueType>> GetJiraIssueTypesAsync(string projectKey)
    {
        var response = await _http.GetAsync($"{_jiraProxyBase}/jira/issuetypes/{projectKey}");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;

        var results = new List<JiraIssueType>();
        if (doc.TryGetProperty("issueTypes", out var types))
        {
            foreach (var t in types.EnumerateArray())
            {
                results.Add(new JiraIssueType
                {
                    Id = t.GetProperty("id").GetString() ?? "",
                    Name = t.GetProperty("name").GetString() ?? ""
                });
            }
        }
        return results;
    }

    public async Task<string?> CreateJiraIssueAsync(
        string projectKey,
        string issueTypeId,
        string summary,
        string? description)
    {
        var body = new
        {
            fields = new
            {
                project = new { key = projectKey },
                issuetype = new { id = issueTypeId },
                summary = summary,
                description = description == null ? null : new
                {
                    type = "doc",
                    version = 1,
                    content = new[]
                    {
                    new
                    {
                        type    = "paragraph",
                        content = new[]
                        {
                            new { type = "text", text = description }
                        }
                    }
                }
                }
            }
        };

        var response = await _http.PostAsync(
            $"{_jiraProxyBase}/jira/issue",
            new StringContent(
                JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8,
                "application/json"));

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json).RootElement;
        return doc.TryGetProperty("key", out var key) ? key.GetString() : null;
    }
}
