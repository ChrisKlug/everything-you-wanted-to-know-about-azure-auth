using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Zkd.Demo.Azure.Auth.Web.Pages.User;

// The signed-in user's picture from Graph.
// No picture, for any reason, means the placeholder. The reason is in the log.
public class PhotoModel(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<PhotoModel> logger) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Placeholder();
        }

        var accessToken = await GetGraphTokenAsync();
        if (accessToken is null)
        {
            logger.LogWarning("No access token to get the profile picture with. The sign-in didn't save one.");
            return Placeholder();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "me/photo/$value");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClientFactory.CreateClient("graph").SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Graph said {Status} to the profile picture request: {Body}",
                (int)response.StatusCode, await response.Content.ReadAsStringAsync());
            return Placeholder();
        }

        Response.Headers.CacheControl = "no-store";
        return File(await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentType?.MediaType ?? "image/jpeg");
    }

    // The token to call Graph with. The sign-in's access token is for my API, so trade the refresh token for one for Graph.
    // In real life, Microsoft.Identity.Web does all of this, and caches the tokens:
    //   return await tokenAcquisition.GetAccessTokenForUserAsync(["User.Read"]);
    // with .AddMicrosoftIdentityWebApp(...).EnableTokenAcquisitionToCallDownstreamApi().AddInMemoryTokenCaches()
    private async Task<string?> GetGraphTokenAsync()
    {
        var refreshToken = await HttpContext.GetTokenAsync("refresh_token");
        if (refreshToken is null)
        {
            logger.LogWarning("No refresh token. The sign-in didn't ask for offline_access.");
            return null;
        }

        var tokenEndpoint = $"https://login.microsoftonline.com/{User.FindFirst("tid")!.Value}/oauth2/v2.0/token";
        using var response = await httpClientFactory.CreateClient().PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = configuration["Entra:ClientId"]!,
            ["client_secret"] = configuration["Entra:ClientSecret"]!,
            ["refresh_token"] = refreshToken,
            ["scope"] = "User.Read",
        }));
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Entra said {Status} to the refresh token: {Body}", (int)response.StatusCode, body);
            return null;
        }

        using var json = System.Text.Json.JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString();
    }

    private IActionResult Placeholder()
    {
        Response.Headers.CacheControl = "no-store";
        return File("/img/placeholder-user.svg", "image/svg+xml");
    }
}
