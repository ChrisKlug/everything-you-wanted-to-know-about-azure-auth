using System.Net.Http.Headers;
using System.Net.Mail;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

// The nightly "your day tomorrow" mail. Everyone who subscribed, their day, one mail each.
public static class Digest
{
    public static async Task<IResult> SendAsync(TokenCredential credential, SqlConnection connection,
        IHttpClientFactory httpClientFactory, IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Digest");
        var tomorrow = DateOnly.FromDateTime(DateTime.Now).AddDays(1);

        var subscribers = await GetSubscribersAsync(connection);
        logger.LogInformation("{Count} subscriber(s) for {Date}", subscribers.Count, tomorrow);

        var sent = 0;
        foreach (var subscriber in subscribers)
        {
            DigestMail.CalendarDay? day;
            try
            {
                day = await GetDayAsync(subscriber, tomorrow, credential, httpClientFactory, configuration, logger);
            }
            catch (AuthenticationFailedException e)
            {
                // The credential's own message ends where Entra's starts. Entra's is the one with the AADSTS code.
                logger.LogWarning("No token for {User}: {Message}", subscriber.UserPrincipalName, e.InnerException?.Message ?? e.Message);
                continue;
            }
            if (day is null)
            {
                continue;
            }

            await SendMailAsync(subscriber, day, configuration);
            logger.LogInformation("Sent the digest to {User}: {Count} event(s) tomorrow", subscriber.UserPrincipalName, day.Events.Count);
            sent++;
        }

        return sent == subscribers.Count
            ? Results.Ok($"Sent {sent} of {subscribers.Count}.")
            : Results.Problem($"Sent {sent} of {subscribers.Count}. The reason is in the log.");
    }

    // Their day, from my Calendar API, as the digest itself.
    private static async Task<DigestMail.CalendarDay?> GetDayAsync(Subscriber subscriber, DateOnly date,
        TokenCredential credential, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger logger)
    {
        // A token for my API, as the digest itself. .default: whatever an admin has granted it.
        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://kickassdev.se/api/.default"]), default);

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"users/{Uri.EscapeDataString(subscriber.UserPrincipalName)}/calendar?date={date:yyyy-MM-dd}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        using var response = await httpClientFactory.CreateClient("calendar-api").SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Calendar API said {Status} for {User}: {Body}",
                (int)response.StatusCode, subscriber.UserPrincipalName, await response.Content.ReadAsStringAsync());
            return null;
        }
        return await response.Content.ReadFromJsonAsync<DigestMail.CalendarDay>();
    }

    private static async Task<List<Subscriber>> GetSubscribersAsync(SqlConnection connection)
    {
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT UserPrincipalName, DisplayName, TenantId FROM Subscribers", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var subscribers = new List<Subscriber>();
        while (await reader.ReadAsync())
        {
            subscribers.Add(new Subscriber(reader.GetString(0), reader.GetString(1), reader.GetGuid(2).ToString()));
        }
        return subscribers;
    }

    // To Mailpit, in Aspire. Nothing leaves the laptop.
    private static async Task SendMailAsync(Subscriber subscriber, DigestMail.CalendarDay day, IConfiguration configuration)
    {
        var smtp = new Uri(configuration["Smtp:Endpoint"]!);
        using var client = new SmtpClient(smtp.Host, smtp.Port);
        var firstName = subscriber.DisplayName.Split(' ')[0];
        using var message = new MailMessage
        {
            From = new MailAddress(configuration["Digest:From"]!),
            Subject = $"Your day tomorrow, {firstName}",
            Body = DigestMail.Render(firstName, day),
            IsBodyHtml = true,
        };
        message.To.Add(new MailAddress(subscriber.UserPrincipalName, subscriber.DisplayName));
        await client.SendMailAsync(message);
    }

    // TenantId: which Entra tenant they belong to. Saved when they subscribe.
    private record Subscriber(string UserPrincipalName, string DisplayName, string TenantId);
}
