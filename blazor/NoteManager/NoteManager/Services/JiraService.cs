using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class JiraExportResult
{
    public bool Success { get; set; }
    public string? IssueKey { get; set; }
    public string? IssueUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

public class JiraService
{
    /*private readonly IHttpClientFactory _httpClientFactory;

    public JiraService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }*/

    /*private HttpClient BuildClient(string baseUrl, string email, string apiToken)
    {
        var client = _httpClientFactory.CreateClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }*/
    private HttpClient BuildClient(string baseUrl, string email, string apiToken)
    {
        var client = new HttpClient();
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(
        string baseUrl, string email, string apiToken)
    {
        try
        {
            var client = BuildClient(baseUrl, email, apiToken);
            var response = await client.GetAsync("rest/api/3/myself");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var displayName = doc.RootElement
                    .GetProperty("displayName").GetString() ?? "Unknown";
                return (true, $"Connected as {displayName}");
            }
            return (false, $"Authentication failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            return (false, $"Connection error: {ex.Message}");
        }
    }

    public async Task<JiraExportResult> CreateIssueAsync(
        string baseUrl, string email, string apiToken,
        string projectKey, string issueType,
        string summary, string body)
    {
        try
        {
            var client = BuildClient(baseUrl, email, apiToken);

            // Build Atlassian Document Format (ADF) for description
            var adf = new
            {
                version = 1,
                type = "doc",
                content = new[]
                {
                    new
                    {
                        type = "paragraph",
                        content = new[]
                        {
                            new { type = "text", text = string.IsNullOrWhiteSpace(body) ? " " : body }
                        }
                    }
                }
            };

            var payload = new
            {
                fields = new
                {
                    project = new { key = projectKey.Trim().ToUpper() },
                    summary = summary,
                    description = adf,
                    issuetype = new { name = issueType }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("rest/api/3/issue", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseBody);
                var key = doc.RootElement.GetProperty("key").GetString();
                var issueUrl = $"{baseUrl.TrimEnd('/')}/browse/{key}";
                return new JiraExportResult
                {
                    Success = true,
                    IssueKey = key,
                    IssueUrl = issueUrl
                };
            }

            // Extract error message from Jira response
            string errorMsg;
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("errorMessages", out var msgs) &&
                    msgs.GetArrayLength() > 0)
                    errorMsg = msgs[0].GetString() ?? responseBody;
                else if (doc.RootElement.TryGetProperty("errors", out var errs))
                    errorMsg = errs.ToString();
                else
                    errorMsg = responseBody;
            }
            catch { errorMsg = responseBody; }

            return new JiraExportResult { Success = false, ErrorMessage = errorMsg };
        }
        catch (Exception ex)
        {
            return new JiraExportResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}