using Microsoft.Extensions.Options;

namespace Zkd.Demo.Azure.Auth.Web.Proxy;

// The browser shows the request, the server sends it. That keeps the client secret out of the browser.
public static class ProxyEndpoints
{
    public const string HttpClientName = "proxy";
    public const string SecretMask = "••••••";

    public static IEndpointRouteBuilder MapProxy(this IEndpointRouteBuilder app)
    {
        app.MapPost("/what-really-happens/send", SendAsync);
        return app;
    }

    private static async Task<IResult> SendAsync(ProxyRequest request, IHttpClientFactory httpClientFactory,
        IOptions<EntraOptions> entraOptions, IOptions<ProxyOptions> proxyOptions, IConfiguration configuration)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return Results.BadRequest("Only absolute https URLs can be sent.");
        }
        if (!proxyOptions.Value.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            return Results.BadRequest($"{uri.Host} isn't in Proxy:AllowedHosts.");
        }

        using var message = new HttpRequestMessage(new HttpMethod(request.Method), uri);
        foreach (var (name, value) in request.Headers ?? [])
        {
            message.Headers.TryAddWithoutValidation(name, value);
        }

        if (request.Form is not null)
        {
            // Only ever hand the secret to Entra.
            var entra = entraOptions.Value;
            var isEntra = uri.Host.Equals(new Uri(entra.Instance).Host, StringComparison.OrdinalIgnoreCase);
            // On-Behalf-Of sends as the Calendar API, client credentials as the Digest. Each with its own secret.
            var clientId = request.Form.GetValueOrDefault("client_id");
            var secret = clientId switch
            {
                { Length: > 0 } when clientId == configuration["CalendarApi:ClientId"] => configuration["CalendarApi:ClientSecret"] ?? "",
                { Length: > 0 } when clientId == configuration["Digest:ClientId"] => configuration["Digest:ClientSecret"] ?? "",
                _ => entra.ClientSecret,
            };
            message.Content = new FormUrlEncodedContent(request.Form.Select(x =>
                x.Key == "client_secret" && x.Value == SecretMask && isEntra
                    ? KeyValuePair.Create(x.Key, secret)
                    : x));
        }

        using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(message);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in response.Headers.Concat(response.Content.Headers))
        {
            headers[name] = string.Join(", ", values);
        }

        // Pictures (Graph /me/photo/$value) can't go through a string. Send them as a data URL the page can show.
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return Results.Ok(new ProxyResponse((int)response.StatusCode, response.ReasonPhrase ?? "", headers,
                $"({bytes.Length} bytes of {contentType})", $"data:{contentType};base64,{Convert.ToBase64String(bytes)}"));
        }

        return Results.Ok(new ProxyResponse((int)response.StatusCode, response.ReasonPhrase ?? "", headers,
            await response.Content.ReadAsStringAsync()));
    }
}

public record ProxyRequest(string Method, string Url, Dictionary<string, string>? Headers, Dictionary<string, string>? Form);

public record ProxyResponse(int Status, string StatusText, Dictionary<string, string> Headers, string Body, string? Image = null);
