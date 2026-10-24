using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Zkd.Demo.Azure.Auth.WhatReallyHappens.Pages.Slides;

public class SlideModel : PageModel
{
    public Slide Slide { get; private set; } = null!;
    public Slide? Previous { get; private set; }
    public Slide? Next { get; private set; }

    public IActionResult OnGet(string name)
    {
        if (SlideDeck.Find(name) is not { } slide)
        {
            return NotFound();
        }
        Slide = slide;
        var step = SlideDeck.ForStep(slide.Step).ToList();
        var index = step.IndexOf(slide);
        Previous = index > 0 ? step[index - 1] : null;
        Next = index < step.Count - 1 ? step[index + 1] : null;
        return Page();
    }
}
