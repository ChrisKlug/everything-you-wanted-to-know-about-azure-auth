using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Zkd.Demo.Azure.Auth.Web.Pages;

// Where a signed-in user from a tenant that isn't a customer ends up. 403: authenticated, but not authorized.
public class MagicWordModel : PageModel
{
    public string? Tenant { get; private set; }

    public void OnGet(string? tenant)
    {
        Tenant = tenant;
        Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}
