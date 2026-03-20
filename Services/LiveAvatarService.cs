namespace AI_Chatbot.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class LiveAvatarService(IHttpClientFactory factory, IConfiguration config)
{
    private readonly string _apiKey = config["LiveAvatar:ApiKey"]!;
    private readonly string _avatarId = config["LiveAvatar:AvatarId"]!;

    public string DefaultAvatarId => _avatarId;

    /// <summary>
    /// Posts to a LiveAvatar endpoint. Uses X-API-KEY header by default,
    /// or Bearer token if sessionToken is supplied (for session-scoped calls).
    /// </summary>
    public async Task<JsonDocument> PostAsync(string path, object body,
        string? sessionToken = null)
    {
        var client = factory.CreateClient("liveavatar");
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        if (sessionToken != null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        else
            req.Headers.Add("X-API-KEY", _apiKey);

        var res = await client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"LiveAvatar {path} → {(int)res.StatusCode}: {text}");

        return JsonDocument.Parse(text);
    }

    /// <summary>
    /// Fetches all pages of the public avatar list.
    /// </summary>
    public async Task<List<JsonElement>> GetPublicAvatarsAsync()
    {
        var client = factory.CreateClient("liveavatar");
        var allAvatars = new List<JsonElement>();
        var nextUrl = "v1/avatars/public?page_size=100";

        while (nextUrl != null)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, nextUrl);
            req.Headers.Add("X-API-KEY", _apiKey);
            var res = await client.SendAsync(req);
            var text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                throw new Exception($"LiveAvatar avatar list → {(int)res.StatusCode}: {text}");

            using var doc = JsonDocument.Parse(text);
            var data = doc.RootElement.GetProperty("data");
            foreach (var avatar in data.GetProperty("results").EnumerateArray())
                allAvatars.Add(avatar.Clone());

            nextUrl = null;
            if (data.TryGetProperty("next", out var next) && next.ValueKind != JsonValueKind.Null)
                nextUrl = next.GetString()!.Replace("https://api.liveavatar.com/", "");
        }

        return allAvatars;
    }
}