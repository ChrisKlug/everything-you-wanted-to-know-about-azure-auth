using System.Text.Json;

// The Azure platform's part, on my laptop. In App Service, Functions and Container Apps the platform runs an endpoint
// like this one, and tells the app where it is with IDENTITY_ENDPOINT and IDENTITY_HEADER. Azure.Identity can't tell the
// difference.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHttpClient();
builder.Services.AddGoogleLogin();

var app = builder.Build();

// The App Service managed identity protocol. The app asks for a token for a resource, as one of its managed identities.
// Which managed identity: by client ID or by resource ID.
app.MapGet("/msi/token", async (string resource, string? client_id, string? mi_res_id, HttpRequest request, GoogleLogin google,
    IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<Program> logger) =>
{
    logger.LogInformation("GET /msi/token{Query}, X-IDENTITY-HEADER: {Header}",
        request.QueryString.Value, request.Headers["X-IDENTITY-HEADER"].ToString());

    // Only what the platform gave the header to gets a token. Stops anyone tricking the app into calling this (SSRF).
    if (request.Headers["X-IDENTITY-HEADER"] != configuration["IDENTITY_HEADER"])
    {
        return Results.Json(new { error = "unauthorized", error_description = "Wrong X-IDENTITY-HEADER." }, statusCode: 401);
    }

    // Azure proves who's asking with the platform's own credentials. I prove it with my Google login: the managed
    // identity has a federated credential that trusts it. Issuer, subject and audience have to match.
    string googleIdToken;
    try
    {
        googleIdToken = await google.GetIdTokenAsync();
    }
    catch (GoogleLoginException e)
    {
        logger.LogWarning("{Message}", e.Message);
        return Results.Json(new { error = "no_google_login", error_description = e.Message }, statusCode: 500);
    }

    // Only on my laptop. Federated credentials don't chain: a managed identity token I got with my Google
    // login can't be another app's credential (AADSTS700231). In Azure the platform issues it, so it can. So when an app
    // asks for a token to use as a credential, it gets my Google login itself, and the app registration trusts Google too.
    if (resource.TrimEnd('/') == "api://AzureADTokenExchange")
    {
        logger.LogInformation("Token for {Resource}: my Google login, {Token}", resource, googleIdToken);
        return Results.Json(new Dictionary<string, string?>
        {
            ["access_token"] = googleIdToken,
            ["expires_on"] = google.ExpiresOn.ToUnixTimeSeconds().ToString(),
            ["resource"] = resource,
            ["token_type"] = "Bearer",
            ["client_id"] = client_id,
            ["mi_res_id"] = mi_res_id,
        });
    }

    // Everything else goes to Entra as the managed identity, which needs its client ID.
    if (client_id is null)
    {
        return Results.Json(new { error = "invalid_request", error_description = "Locally, only client_id picks a managed identity for this resource." }, statusCode: 400);
    }

    var tokenEndpoint = $"https://login.microsoftonline.com/{configuration["Entra:TenantId"]}/oauth2/v2.0/token";
    using var response = await httpClientFactory.CreateClient().PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"] = "client_credentials",
        ["client_id"] = client_id,
        ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
        ["client_assertion"] = googleIdToken,
        ["scope"] = $"{resource.TrimEnd('/')}/.default",
    }));
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
    {
        logger.LogWarning("Entra said {Status}: {Body}", (int)response.StatusCode, body);
        return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
    }

    using var json = JsonDocument.Parse(body);
    var accessToken = json.RootElement.GetProperty("access_token").GetString();
    var expiresIn = json.RootElement.GetProperty("expires_in").GetInt32();
    logger.LogInformation("Token for {Resource}: {Token}", resource, accessToken);

    // What App Service answers with. expires_on is seconds since 1970.
    return Results.Json(new Dictionary<string, string?>
    {
        ["access_token"] = accessToken,
        ["expires_on"] = DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToUnixTimeSeconds().ToString(),
        ["resource"] = resource,
        ["token_type"] = "Bearer",
        ["client_id"] = client_id,
    });
});

app.MapGoogleLogin();
app.MapDefaultEndpoints();

app.Run();
