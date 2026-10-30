using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Zkd.Demo.Azure.Auth.Web.Pages;

// Where a signed-in user from a tenant that isn't a customer ends up. 403: authenticated, but not authorized.
// They never got a cookie, so the tenant comes from the URL. Once it's on the list, they sign in again, for real.
public class MagicWordModel(IConfiguration configuration) : PageModel
{
    public string? Tenant { get; private set; }

    public IActionResult OnGet(string? tenant)
    {
        if (tenant is not null && configuration["Customers:TenantIds"]?.Split(',').Contains(tenant) == true)
        {
            return Redirect("/login");
        }

        Tenant = tenant;
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Page();
    }
}
