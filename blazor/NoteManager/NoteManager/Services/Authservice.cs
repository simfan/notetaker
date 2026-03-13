using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;

namespace NoteManager.Services;

public class SupabaseSession
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public SupabaseUser? User { get; set; }
}

public class SupabaseUser
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public SupabaseSession? Session { get; set; }
}

public class AuthService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly IJSRuntime _js;
    private readonly string _supabaseUrl;
    private readonly string _anonKey;

    private SupabaseSession? _session;

    public SupabaseSession? Session => _session;
    public string? UserId => _session?.User?.Id;
    public bool IsAuthenticated => _session != null && !string.IsNullOrEmpty(_session.AccessToken);

    public AuthService(HttpClient http, IConfiguration config, IJSRuntime js)
    {
        _http = http;
        _config = config;
        _js = js;
        _supabaseUrl = _config["Supabase:Url"]!;
        _anonKey = _config["Supabase:AnonKey"]!;
    }

    // ── Session persistence ───────────────────────────────────

    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", "nk_session");
            if (string.IsNullOrEmpty(json)) return false;

            var session = JsonSerializer.Deserialize<SupabaseSession>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (session == null || string.IsNullOrEmpty(session.AccessToken)) return false;

            // Try to refresh the session to verify it's still valid
            var refreshed = await RefreshSessionAsync(session.RefreshToken);
            if (refreshed.Success && refreshed.Session != null)
            {
                _session = refreshed.Session;
                await PersistSessionAsync();
                return true;
            }

            await ClearSessionAsync();
            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task PersistSessionAsync()
    {
        if (_session == null) return;
        var json = JsonSerializer.Serialize(_session);
        await _js.InvokeVoidAsync("localStorage.setItem", "nk_session", json);
    }

    private async Task ClearSessionAsync()
    {
        _session = null;
        await _js.InvokeVoidAsync("localStorage.removeItem", "nk_session");
    }

    // ── Email / Password ──────────────────────────────────────

    public async Task<AuthResult> SignInWithPasswordAsync(string email, string password)
    {
        try
        {
            var payload = new { email, password };
            var req = new HttpRequestMessage(HttpMethod.Post,
                $"{_supabaseUrl}/auth/v1/token?grant_type=password")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("apikey", _anonKey);

            var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                var err = TryParseError(body);
                return new AuthResult { Success = false, ErrorMessage = err };
            }

            _session = ParseSession(body);
            await PersistSessionAsync();
            return new AuthResult { Success = true, Session = _session };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<AuthResult> SignUpAsync(string email, string password)
    {
        try
        {
            var payload = new { email, password };
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/auth/v1/signup")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("apikey", _anonKey);

            var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return new AuthResult { Success = false, ErrorMessage = TryParseError(body) };

            // If email confirmation is disabled, session is returned immediately
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("access_token", out _))
            {
                _session = ParseSession(body);
                await PersistSessionAsync();
                return new AuthResult { Success = true, Session = _session };
            }

            // Email confirmation required
            return new AuthResult { Success = true, ErrorMessage = "confirm_email" };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ── OAuth ─────────────────────────────────────────────────

    public string GetOAuthUrl(string provider)
    {
        var redirectTo = Uri.EscapeDataString("http://localhost:7222/auth/callback");
        return $"{_supabaseUrl}/auth/v1/authorize?provider={provider}&redirect_to={redirectTo}";
    }

    public async Task<AuthResult> HandleOAuthCallbackAsync(string accessToken, string refreshToken)
    {
        try
        {
            // Get user info using the access token
            var req = new HttpRequestMessage(HttpMethod.Get, $"{_supabaseUrl}/auth/v1/user");
            req.Headers.Add("apikey", _anonKey);
            req.Headers.Add("Authorization", $"Bearer {accessToken}");

            var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return new AuthResult { Success = false, ErrorMessage = TryParseError(body) };

            using var doc = JsonDocument.Parse(body);
            var userId = doc.RootElement.GetProperty("id").GetString() ?? "";
            var email = doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "";

            _session = new SupabaseSession
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                User = new SupabaseUser { Id = userId, Email = email }
            };

            await PersistSessionAsync();
            return new AuthResult { Success = true, Session = _session };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ── Refresh ───────────────────────────────────────────────

    private async Task<AuthResult> RefreshSessionAsync(string refreshToken)
    {
        try
        {
            var payload = new { refresh_token = refreshToken };
            var req = new HttpRequestMessage(HttpMethod.Post,
                $"{_supabaseUrl}/auth/v1/token?grant_type=refresh_token")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("apikey", _anonKey);

            var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return new AuthResult { Success = false };

            return new AuthResult { Success = true, Session = ParseSession(body) };
        }
        catch
        {
            return new AuthResult { Success = false };
        }
    }

    // ── Sign Out ──────────────────────────────────────────────

    public async Task SignOutAsync()
    {
        try
        {
            if (_session != null)
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/auth/v1/logout");
                req.Headers.Add("apikey", _anonKey);
                req.Headers.Add("Authorization", $"Bearer {_session.AccessToken}");
                await _http.SendAsync(req);
            }
        }
        finally
        {
            await ClearSessionAsync();
        }
    }

    // ── Helpers ───────────────────────────────────────────────

    private static SupabaseSession ParseSession(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString() ?? "";
        var refreshToken = root.TryGetProperty("refresh_token", out var rt)
            ? rt.GetString() ?? "" : "";

        string userId = "", email = "";
        if (root.TryGetProperty("user", out var userEl))
        {
            userId = userEl.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
            email = userEl.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
        }

        return new SupabaseSession
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new SupabaseUser { Id = userId, Email = email }
        };
    }

    private static string TryParseError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error_description", out var ed))
                return ed.GetString() ?? "Unknown error";
            if (doc.RootElement.TryGetProperty("msg", out var msg))
                return msg.GetString() ?? "Unknown error";
            if (doc.RootElement.TryGetProperty("message", out var m))
                return m.GetString() ?? "Unknown error";
        }
        catch { }
        return "Unknown error";
    }
}