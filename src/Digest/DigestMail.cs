using System.Globalization;
using System.Text;
using static System.Net.WebUtility;

// The mail itself. Inline styles only: mail clients ignore stylesheets. Same logo and colours as the web.
public static class DigestMail
{
    private const string Orange = "#e8590c";
    private const string Ink = "#1f2937";
    private const string Muted = "#6b7280";

    private const string Logo = """
        <svg width="40" height="40" viewBox="0 0 40 40" style="vertical-align:middle">
          <rect x="3" y="7" width="34" height="30" rx="5" fill="#fff" stroke="#1f2937" stroke-width="2.5" />
          <path d="M3 12a5 5 0 0 1 5-5h24a5 5 0 0 1 5 5v4H3z" fill="#e8590c" stroke="#1f2937" stroke-width="2.5" />
          <rect x="11" y="3" width="3" height="8" rx="1.5" fill="#1f2937" />
          <rect x="26" y="3" width="3" height="8" rx="1.5" fill="#1f2937" />
          <path d="M22 19l-7 9h5l-2 6 8-10h-5z" fill="#e8590c" stroke="#1f2937" stroke-width="1.5" stroke-linejoin="round" />
        </svg>
        """;

    public static string Render(string firstName, CalendarDay day)
    {
        var date = day.Date.ToDateTime(TimeOnly.MinValue).ToString("dddd d MMMM", CultureInfo.GetCultureInfo("en-GB"));

        var rows = new StringBuilder();
        foreach (var e in day.Events)
        {
            var location = string.IsNullOrEmpty(e.Location)
                ? ""
                : $"""<div style="color:{Muted};font-size:14px;margin-top:2px">{HtmlEncode(e.Location)}</div>""";
            rows.Append($"""
                <tr>
                  <td style="padding:10px 16px 10px 0;vertical-align:top;white-space:nowrap;font-family:Menlo,Consolas,monospace;font-size:15px;color:{Muted}">{e.Start}&ndash;{e.End}</td>
                  <td style="padding:10px 0 10px 14px;border-left:4px solid {Orange};vertical-align:top">
                    <div style="font-size:17px;font-weight:600">{HtmlEncode(e.Subject)}</div>{location}
                  </td>
                </tr>
                """);
        }
        if (day.Events.Count == 0)
        {
            rows.Append($"""<tr><td style="padding:10px 0;color:{Muted}">Nothing tomorrow. Suspicious.</td></tr>""");
        }

        return $"""
            <!doctype html>
            <html>
            <body style="margin:0;padding:0;background:#f3f4f6">
              <div style="max-width:600px;margin:0 auto;padding:32px 16px;font-family:-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:{Ink}">
                <div style="background:#fff;border-top:6px solid {Orange};border-radius:8px;padding:28px 32px">
                  <div style="font-size:20px;font-weight:700">{Logo}<span style="vertical-align:middle;margin-left:10px">Kick-Ass Calendar</span></div>
                  <h1 style="font-size:28px;margin:28px 0 4px">Your day tomorrow, {HtmlEncode(firstName)}</h1>
                  <div style="color:{Muted};font-size:16px;margin-bottom:20px">{date}</div>
                  <table style="border-collapse:collapse;width:100%">{rows}</table>
                </div>
                <div style="color:{Muted};font-size:13px;text-align:center;margin-top:16px">The nightly digest. Sent by an app, with nobody signed in.</div>
              </div>
            </body>
            </html>
            """;
    }

    // What the Calendar API's /users/{id}/calendar returns.
    public record CalendarDay(DateOnly Date, string TimeZone, List<CalendarEvent> Events);

    public record CalendarEvent(string Subject, string Start, string End, string? Location);
}
