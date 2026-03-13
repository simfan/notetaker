using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JiraProxy;

var builder = WebApplication.CreateBuilder(args);

// ?? Services ????????????????????????????????????????????????????????????????
builder.Services.AddSingleton<JiraTokenStore>();
builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorOrigin", policy =>
        policy.WithOrigins("http://localhost:5049", "https://localhost:7222")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("BlazorOrigin");

// ?? Config helpers ???????????????????????????????????????????????????????????
var config = app.Configuration;
var clientId = config["Jira:ClientId"]!;
var clientSecret = config["Jira:ClientSecret"]!;
var callbackUrl = config["Jira:CallbackUrl"]!;
var scopes = config["Jira:Scopes"]!;
var authBase = config["Jira:AuthBaseUrl"]!;
var apiBase = config["Jira:ApiBaseUrl"]!;

// ?? GET /jira/status ?????????????????????????????????????????????????????????
app.MapGet("/jira/status", (JiraTokenStore store) =>
{
    return Results.Ok(new { connected = store.IsConnected });
});

// ?? GET /jira/auth ???????????????????????????????????????????????????????????
// Redirects the browser to Atlassian's OAuth consent screen
app.MapGet("/jira/auth", (JiraTokenStore store) =>
{
    var url = $"{authBase}/authorize" +
              $"?audience=api.atlassian.com" +
              $"&client_id={Uri.EscapeDataString(clientId)}" +
              $"&scope={Uri.EscapeDataString(scopes)}" +
              $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
              $"&response_type=code" +
              $"&prompt=consent";

    return Results.Redirect(url);
});

// ?? GET /jira/callback ???????????????????????????????????????????????????????
// Atlassian redirects here after user approves; exchanges code for tokens
app.MapGet("/jira/callback", async (
    string code,
    JiraTokenStore store,
    IHttpClientFactory factory) =>
{
    var http = factory.CreateClient();

    // Exchange auth code for tokens
    var tokenRequest = new
    {
        grant_type = "authorization_code",
        client_id = clientId,
        client_secret = clientSecret,
        code = code,
        redirect_uri = callbackUrl
    };

    var tokenResponse = await http.PostAsync(
        $"{authBase}/oauth/token",
        new StringContent(
            JsonSerializer.Serialize(tokenRequest),
            Encoding.UTF8,
            "application/json"));

    if (!tokenResponse.IsSuccessStatusCode)
    {
        var err = await tokenResponse.Content.ReadAsStringAsync();
        return Results.Problem($"Token exchange failed: {err}");
    }

    var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
    var tokenDoc = JsonDocument.Parse(tokenJson).RootElement;

    store.AccessToken = tokenDoc.GetProperty("access_token").GetString();
    store.RefreshToken = tokenDoc.TryGetProperty("refresh_token", out var rt)
                            ? rt.GetString() : null;
    store.ExpiresAt = DateTime.UtcNow.AddSeconds(
                            tokenDoc.GetProperty("expires_in").GetInt32());

    // Fetch the Atlassian cloud ID (needed for all API calls)
    http.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", store.AccessToken);

    var sitesResponse = await http.GetAsync($"{apiBase}/oauth/token/accessible-resources");
    var sitesJson = await sitesResponse.Content.ReadAsStringAsync();
    var sitesDoc = JsonDocument.Parse(sitesJson).RootElement;

    if (sitesDoc.ValueKind == JsonValueKind.Array && sitesDoc.GetArrayLength() > 0)
        store.CloudId = sitesDoc[0].GetProperty("id").GetString();

    // Redirect back to Blazor app with success flag
    return Results.Redirect("http://localhost:5049?jira=connected");
});

// ?? GET /jira/projects ???????????????????????????????????????????????????????
app.MapGet("/jira/projects", async (
    JiraTokenStore store,
    IHttpClientFactory factory) =>
{
    if (!store.IsConnected)
        return Results.Unauthorized();

    var http = factory.CreateClient();
    http.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", store.AccessToken);

    var response = await http.GetAsync(
        $"{apiBase}/ex/jira/{store.CloudId}/rest/api/3/project/search?maxResults=50");

    var body = await response.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json");
});

// ?? GET /jira/issuetypes/{projectKey} ????????????????????????????????????????
app.MapGet("/jira/issuetypes/{projectKey}", async (
    string projectKey,
    JiraTokenStore store,
    IHttpClientFactory factory) =>
{
    if (!store.IsConnected)
        return Results.Unauthorized();

    var http = factory.CreateClient();
    http.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", store.AccessToken);

    var response = await http.GetAsync(
        $"{apiBase}/ex/jira/{store.CloudId}/rest/api/3/project/{projectKey}?expand=issueTypes");

    var body = await response.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json");
});

// ?? POST /jira/issue ?????????????????????????????????????????????????????????
app.MapPost("/jira/issue", async (
    JsonElement payload,
    JiraTokenStore store,
    IHttpClientFactory factory) =>
{
    if (!store.IsConnected)
        return Results.Unauthorized();

    var http = factory.CreateClient();
    http.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", store.AccessToken);

    var response = await http.PostAsync(
        $"{apiBase}/ex/jira/{store.CloudId}/rest/api/3/issue",
        new StringContent(
            payload.GetRawText(),
            Encoding.UTF8,
            "application/json"));

    var body = await response.Content.ReadAsStringAsync();
    return response.IsSuccessStatusCode
        ? Results.Content(body, "application/json")
        : Results.Problem(body);
});

app.Run();