using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Builder;

// The user's day, from their Outlook calendar.
public static class CalendarEndpoints
{
    public static async Task<CalendarDay> GetCalendarAsync(DateOnly? date, HttpContext context,
        IHttpClientFactory httpClientFactory, IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("CalendarEndpoints");
        var accessToken = await GetGraphTokenAsync(context, httpClientFactory, configuration, logger);
        return await GetDayAsync("me", accessToken, date, httpClientFactory, configuration, logger);
    }

    public static async Task<CalendarDay> GetUserCalendarAsync(string id, DateOnly? date,
        IHttpClientFactory httpClientFactory, IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("CalendarEndpoints");
        var accessToken = await GetAppGraphTokenAsync(httpClientFactory, configuration, logger);
        return await GetDayAsync($"users/{Uri.EscapeDataString(id)}", accessToken, date, httpClientFactory, configuration, logger);
    }

    private static async Task<CalendarDay> GetDayAsync(string user, string? accessToken, DateOnly? date,
        IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger logger)
    {
        // Today is the machine's today. The day starts and ends in the calendar's time zone.
        var timeZone = configuration["Calendar:TimeZone"] ?? "UTC";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var day = date ?? DateOnly.FromDateTime(DateTime.Now);
        var start = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);

        // No events, for any reason, means an empty day. The reason is in the log.
        if (accessToken is null)
        {
            return new CalendarDay(day, timeZone, []);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{user}/calendarView?startDateTime={start:yyyy-MM-ddTHH:mm:ssZ}&endDateTime={end:yyyy-MM-ddTHH:mm:ssZ}" +
            "&$orderby=start/dateTime&$select=subject,start,end,location&$top=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Prefer", $"outlook.timezone=\"{timeZone}\"");

        using var response = await httpClientFactory.CreateClient("graph").SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Graph said {Status} to the calendar request: {Body}",
                (int)response.StatusCode, await response.Content.ReadAsStringAsync());
            return new CalendarDay(day, timeZone, []);
        }

        var events = await response.Content.ReadFromJsonAsync<GraphPage>();
        return new CalendarDay(day, timeZone, events!.Value.Select(e => new CalendarEvent(
            e.Subject, e.Start.DateTime[11..16], e.End.DateTime[11..16], e.Location?.DisplayName)).ToList());
    }

    // The token to call Graph with: the one the caller sent.
    private static Task<string?> GetGraphTokenAsync(HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger logger) => Task.FromResult(AuthenticationHeaderValue.Parse(context.Request.Headers.Authorization!).Parameter);

    // The token to call Graph with when nobody is signed in: my own, with what an admin granted me. Client credentials.
    private static async Task<string?> GetAppGraphTokenAsync(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger logger)
    {
        // In real life, Microsoft.Identity.Web does all of this, and caches the token:
        //   return await tokenAcquisition.GetAccessTokenForAppAsync("https://graph.microsoft.com/.default");
        // with .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("Entra")).EnableTokenAcquisitionToCallDownstreamApi().AddInMemoryTokenCaches()

        var tokenEndpoint = $"https://login.microsoftonline.com/{configuration["Entra:TenantId"]}/oauth2/v2.0/token";
        using var response = await httpClientFactory.CreateClient().PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = configuration["Entra:ClientId"]!,
            ["client_secret"] = configuration["Entra:ClientSecret"]!,
            ["scope"] = "https://graph.microsoft.com/.default",
        }));
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Entra said {Status} to the client credentials request: {Body}", (int)response.StatusCode, body);
            return null;
        }

        using var json = System.Text.Json.JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString();
    }

    public record CalendarDay(DateOnly Date, string TimeZone, List<CalendarEvent> Events);

    public record CalendarEvent(string Subject, string Start, string End, string? Location);

    private record GraphPage([property: JsonPropertyName("value")] List<GraphEvent> Value);

    private record GraphEvent(string Subject, GraphDateTime Start, GraphDateTime End, GraphLocation? Location);

    private record GraphDateTime(string DateTime);

    private record GraphLocation(string? DisplayName);
}
