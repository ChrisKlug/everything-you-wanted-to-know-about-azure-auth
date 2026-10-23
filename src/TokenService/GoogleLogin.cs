using System.Text.Json;

// My Google login, kept alive with a refresh token. Google ID tokens live an hour, and Entra only accepts federated
// tokens that live at most 25 hours (Facebook's live 90 days, so Entra turns those down). The refresh token gets a new
// one whenever it's needed, so one sign-in before the talk is enough.
public class GoogleLogin(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GoogleLogin> logger)
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private readonly string _refreshTokenFile = configuration["Google:RefreshTokenFile"]
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "kickass-demo", "google-refresh-token");

    private string? _idToken;
    private DateTimeOffset _expiresOn;

    // When the ID token GetIdTokenAsync returned last runs out.
    public DateTimeOffset ExpiresOn => _expiresOn;

    public async Task<string> GetIdTokenAsync()
    {
        if (_idToken is not null && _expiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return _idToken;
        }

        if (!File.Exists(_refreshTokenFile))
        {
            throw new GoogleLoginException("Not signed in to Google. Open /google/login on the token service.");
        }

        var json = await PostAsync(new()
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = (await File.ReadAllTextAsync(_refreshTokenFile)).Trim(),
        });
        _idToken = json.GetProperty("id_token").GetString()!;
        _expiresOn = DateTimeOffset.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32());
        logger.LogInformation("New Google ID token: {Token}", _idToken);
        return _idToken;
    }

    public string LoginUrl(string callbackUrl) =>
        "https://accounts.google.com/o/oauth2/v2/auth" +
        $"?client_id={Uri.EscapeDataString(configuration["Google:ClientId"]!)}" +
        $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
        "&response_type=code&scope=openid&access_type=offline&prompt=consent";

    public async Task CompleteLoginAsync(string code, string callbackUrl)
    {
        var json = await PostAsync(new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = callbackUrl,
        });
        Directory.CreateDirectory(Path.GetDirectoryName(_refreshTokenFile)!);
        await File.WriteAllTextAsync(_refreshTokenFile, json.GetProperty("refresh_token").GetString());
        File.SetUnixFileMode(_refreshTokenFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        _idToken = null;
    }

    private async Task<JsonElement> PostAsync(Dictionary<string, string> form)
    {
        form["client_id"] = configuration["Google:ClientId"]!;
        form["client_secret"] = configuration["Google:ClientSecret"]!;
        using var response = await httpClientFactory.CreateClient().PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new GoogleLoginException($"Google said {(int)response.StatusCode}: {body}");
        }
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}

public class GoogleLoginException(string message) : Exception(message);

public static class GoogleLoginExtensions
{
    public static IServiceCollection AddGoogleLogin(this IServiceCollection services) => services.AddSingleton<GoogleLogin>();

    // Once per machine, or if the refresh token dies: sign in to Google, and the refresh token is saved for next time.
    public static void MapGoogleLogin(this WebApplication app)
    {
        app.MapGet("/google/login", (HttpRequest request, GoogleLogin google) => Results.Redirect(google.LoginUrl(CallbackUrl(request))));
        app.MapGet("/google/callback", async (string code, HttpRequest request, GoogleLogin google) =>
        {
            await google.CompleteLoginAsync(code, CallbackUrl(request));
            return Results.Text("Signed in to Google. The refresh token is saved, you can close this.");
        });
    }

    private static string CallbackUrl(HttpRequest request) => $"{request.Scheme}://{request.Host}/google/callback";
}
