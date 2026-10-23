using System.Net.Http.Headers;
using System.Text.Json.Serialization;

// Someone's day, straight from Graph, like the Calendar API does it. Only the token is the digest's business.
public static class CalendarGraph
{
    public static async Task<DigestMail.CalendarDay?> GetDayAsync(string userPrincipalName, DateOnly date, string accessToken,
        IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger logger)
    {
        logger.LogInformation("Graph token for {User}: {Token}", userPrincipalName, accessToken);

        // The day starts and ends in the calendar's time zone.
        var timeZone = configuration["Calendar:TimeZone"] ?? "UTC";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var start = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(userPrincipalName)}/calendarView?startDateTime={start:yyyy-MM-ddTHH:mm:ssZ}&endDateTime={end:yyyy-MM-ddTHH:mm:ssZ}" +
            "&$orderby=start/dateTime&$select=subject,start,end,location&$top=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Prefer", $"outlook.timezone=\"{timeZone}\"");
        using var response = await httpClientFactory.CreateClient().SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Graph said {Status} for {User}: {Body}",
                (int)response.StatusCode, userPrincipalName, await response.Content.ReadAsStringAsync());
            return null;
        }

        var events = await response.Content.ReadFromJsonAsync<GraphPage>();
        return new DigestMail.CalendarDay(date, timeZone, events!.Value.Select(e => new DigestMail.CalendarEvent(
            e.Subject, e.Start.DateTime[11..16], e.End.DateTime[11..16], e.Location?.DisplayName)).ToList());
    }

    private record GraphPage([property: JsonPropertyName("value")] List<GraphEvent> Value);

    private record GraphEvent(string Subject, GraphDateTime Start, GraphDateTime End, GraphLocation? Location);

    private record GraphDateTime(string DateTime);

    private record GraphLocation(string? DisplayName);
}
