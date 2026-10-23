using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Zkd.Demo.Azure.Auth.Web.Pages;

// [Authorize] does the work: not signed in means a challenge, which is the sign-in. Then back to the start page.
[Authorize]
public class LoginModel : PageModel
{
    public IActionResult OnGet() => Redirect("/");
}
