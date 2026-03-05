using System.Text.Json.Serialization;

namespace NoteManager.Models;

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

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public Group? Group { get; set; }
    public List<Tag> Tags { get; set; } = new();
}

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

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}