using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Zkd.Demo.Azure.Auth.Web.Pages.WhatReallyHappens;

// Entra posts back here with response_mode=form_post. That post doesn't carry an antiforgery token.
[IgnoreAntiforgeryToken]
public class IndexModel(IOptions<EntraOptions> options, IConfiguration configuration) : PageModel
{
    public EntraOptions Entra { get; } = options.Value;

    public bool IsConfigured => !string.IsNullOrEmpty(Entra.TenantId) && !string.IsNullOrEmpty(Entra.ClientId);

    // The API's Application ID URI is on the verified domain, so it's the same on every run.
    public string CalendarApiScope => "https://kickassdev.se/api/KickAssCalendar.Read";

    // Everything the web is registered for on the API, plus what the API needs downstream.
    public string CalendarApiDefaultScope => "https://kickassdev.se/api/.default";

    // Scopes that are whole URIs show the last part. .default needs the part before it to say whose it is.
    public static string ScopeLabel(string scope) =>
        scope.EndsWith("/.default") ? string.Join('/', scope.Split('/')[^2..]) : scope.Split('/')[^1];

    // What an access token for the Calendar API has as its audience.
    public string? CalendarApiClientId => configuration["CalendarApi:ClientId"];

    // The Calendar API's address, from Aspire's service discovery.
    public string? CalendarApiUrl => configuration["services:calendar-api:https:0"]?.TrimEnd('/');

    // The nightly digest: an app with no user, and the app role it needs on the API.
    public string? DigestClientId => configuration["Digest:ClientId"];

    public string CalendarApiAppRoleScope => "https://kickassdev.se/api/KickAssCalendar.Read.All";

    public string RedirectUri => $"{Request.Scheme}://{Request.Host}/what-really-happens/callback";

    public Dictionary<string, string>? PostedParameters { get; private set; }

    public void OnGet()
    {
    }

    public void OnPost()
    {
        PostedParameters = Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString());
    }
}
