using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Zkd.Demo.Azure.Auth.Web.Pages;

// Only forgets the sign-in here. Entra still knows the user, so signing in again is quick.
// That's what's needed when the sign-in settings change: a new sign-in, with the new scopes.
public class LogoutModel : PageModel
{
    public IActionResult OnGet() => SignOut(new AuthenticationProperties { RedirectUri = "/" }, "Cookies");
}
