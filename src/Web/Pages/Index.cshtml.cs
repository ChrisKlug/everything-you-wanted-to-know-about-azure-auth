using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Zkd.Demo.Azure.Auth.Web.Pages;

// Signed out: the landing page. Signed in: the user's day, from the Kick-Ass Calendar API, with whatever access token
// the sign-in saved. If the API says no, the page just says it's loading. The reason is in the log.
public class IndexModel(IHttpClientFactory httpClientFactory, ILogger<IndexModel> logger) : PageModel
{
    public const int FirstHour = 7;
    public const int LastHour = 19;

    public DateOnly Date { get; private set; }
    public CalendarDay? Day { get; private set; }

    public async Task OnGetAsync(DateOnly? date)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        Date = date ?? DateOnly.FromDateTime(DateTime.Now);
        var accessToken = await HttpContext.GetTokenAsync("access_token");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"calendar?date={Date:yyyy-MM-dd}");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        using var response = await httpClientFactory.CreateClient("calendar-api").SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var challenge = response.Headers.WwwAuthenticate.ToString();
            logger.LogWarning("The Calendar API said {Status} to the calendar request: {Detail}", (int)response.StatusCode,
                challenge.Length > 0 ? $"WWW-Authenticate: {challenge}" : await response.Content.ReadAsStringAsync());
            return;
        }

        Day = await response.Content.ReadFromJsonAsync<CalendarDay>();
    }

    // Where an event goes in the day view, in hours from FirstHour. Clamped to the visible hours.
    public static (double Top, double Height) Position(CalendarEvent item)
    {
        static double Hours(string time) =>
            Math.Clamp(TimeOnly.Parse(time).ToTimeSpan().TotalHours, FirstHour, LastHour) - FirstHour;
        var top = Hours(item.Start);
        return (top, Math.Max(Hours(item.End) - top, 0.5));
    }

    public record CalendarDay(DateOnly Date, string TimeZone, List<CalendarEvent> Events);

    public record CalendarEvent(string Subject, string Start, string End, string? Location);
}
