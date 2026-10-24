using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Zkd.Demo.Azure.Auth.WhatReallyHappens.Pages;

// Where Entra sends the browser back to, for every step. It notes what came back and returns to the step that asked.
// Entra posts here with response_mode=form_post, and that post doesn't carry an antiforgery token.
[IgnoreAntiforgeryToken]
public class CallbackModel : PageModel
{
    public Dictionary<string, string>? Posted { get; private set; }

    public void OnGet()
    {
    }

    public void OnPost()
    {
        Posted = Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString());
    }
}
