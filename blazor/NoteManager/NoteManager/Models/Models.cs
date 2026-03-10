using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoteManager.Models;

// ── Note Type Enum ───────────────────────────────────────────
public enum NoteType
{
    Note,
    Task,
    Assignment
}

// ── Template Type Enum ───────────────────────────────────────
public enum GroupTemplateType
{
    Standard,
    Task,
    Assignment,
    DevNote,
    Link
}

// ── Note Metadata (per-template extra fields) ────────────────
public class NoteMetadata
{
    // Task + Assignment
    [JsonPropertyName("is_complete")]
    public bool? IsComplete { get; set; }

    // Assignment only
    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }

    // Dev Note
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
    // Link re-uses Url above
}

// ── Note ────────────────────────────────────────────────────
public class Note
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("group_id")]
    public string? GroupId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "Untitled Note";

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("photo_url")]
    public string? PhotoUrl { get; set; }

    [JsonPropertyName("note_type")]
    public string NoteTypeRaw { get; set; } = "note";

    [JsonPropertyName("note_metadata")]
    public JsonElement NoteMetadataRaw { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public Group? Group { get; set; }
    public List<Tag> Tags { get; set; } = new();

    // Parsed convenience property
    public NoteType NoteType => NoteTypeRaw switch
    {
        "task" => NoteType.Task,
        "assignment" => NoteType.Assignment,
        _ => NoteType.Note
    };

    public string NoteTypeIcon => NoteType switch
    {
        NoteType.Task => "☑️",
        NoteType.Assignment => "📅",
        _ => "📄"
    };

    // Convenience accessor — deserializes NoteMetadataRaw on demand
    private NoteMetadata? _metadata;
    public NoteMetadata Metadata
    {
        get
        {
            if (_metadata != null) return _metadata;
            try
            {
                _metadata = NoteMetadataRaw.ValueKind == JsonValueKind.Object
                    ? JsonSerializer.Deserialize<NoteMetadata>(
                          NoteMetadataRaw.GetRawText(),
                          new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? new NoteMetadata()
                    : new NoteMetadata();
            }
            catch
            {
                _metadata = new NoteMetadata();
            }
            return _metadata;
        }
    }

    public void InvalidateMetadataCache() => _metadata = null;
}

// ── Tag ──────────────────────────────────────────────────────
public class Tag
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#6366f1";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

// ── Group ────────────────────────────────────────────────────
public class Group
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#6366f1";

    [JsonPropertyName("is_public")]
    public bool IsPublic { get; set; } = false;
    [JsonPropertyName("template_type")]
    public string TemplateTypeRaw { get; set; } = "standard";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    // Parsed convenience property
    public GroupTemplateType TemplateType => TemplateTypeRaw switch
    {
        "task" => GroupTemplateType.Task,
        "assignment" => GroupTemplateType.Assignment,
        "dev_note" => GroupTemplateType.DevNote,
        "link" => GroupTemplateType.Link,
        _ => GroupTemplateType.Standard
    };

    public string TemplateIcon => TemplateType switch
    {
        GroupTemplateType.Task => "☑️",
        GroupTemplateType.Assignment => "📅",
        GroupTemplateType.DevNote => "💻",
        GroupTemplateType.Link => "🔗",
        _ => "📄"
    };
}

// ── TagGroupRule ─────────────────────────────────────────────
public class TagGroupRule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("tag_id")]
    public string TagId { get; set; } = "";

    [JsonPropertyName("group_id")]
    public string GroupId { get; set; } = "";

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    public Tag? Tag { get; set; }
    public Group? Group { get; set; }
}

// ── Project ──────────────────────────────────────────────────
public class Project
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("tag_id")]
    public string? TagId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    public Tag? Tag { get; set; }
}