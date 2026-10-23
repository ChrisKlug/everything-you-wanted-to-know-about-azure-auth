using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Zkd.Demo.Azure.Auth.Web.Pages;

// Where a customer's admin turns on the nightly digest: admin consent for the Digest registration, in their own
// tenant. Entra sends the admin back here with the answer.
[Authorize]
public class DigestModel(IConfiguration configuration) : PageModel
{
    public string ConsentUrl { get; private set; } = "";
    public string? Tenant { get; private set; }
    public bool TurnedOn { get; private set; }
    public string? Error { get; private set; }

    public void OnGet(string? admin_consent, string? tenant, string? error, string? error_description)
    {
        // tid, or its long name if the sign-in maps Entra's claim names.
        Tenant = User.FindFirst("tid")?.Value ?? User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
        TurnedOn = admin_consent == "True";
        Error = error is null ? null : $"{error}: {error_description}";

        // .default: everything the Digest registration asks for, which is application Calendars.Read.
        ConsentUrl = $"https://login.microsoftonline.com/{Tenant}/v2.0/adminconsent" +
            $"?client_id={Uri.EscapeDataString(configuration["Digest:ClientId"]!)}" +
            $"&scope={Uri.EscapeDataString("https://graph.microsoft.com/.default")}" +
            $"&redirect_uri={Uri.EscapeDataString($"{Request.Scheme}://{Request.Host}/digest")}";
    }
}
