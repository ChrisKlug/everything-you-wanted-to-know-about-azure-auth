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

    // The token to call Graph with: whatever access token the sign-in saved.
    private async Task<string?> GetGraphTokenAsync() => await HttpContext.GetTokenAsync("access_token");

    private IActionResult Placeholder()
    {
        Response.Headers.CacheControl = "no-store";
        return File("/img/placeholder-user.svg", "image/svg+xml");
    }
}
