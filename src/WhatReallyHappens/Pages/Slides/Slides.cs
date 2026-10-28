namespace Zkd.Demo.Azure.Auth.WhatReallyHappens.Pages.Slides;

// "This is what happened": a title, a diagram and a few points, after a step's requests. Same CSS as the rest,
// so it's a URL, not a jump to PowerPoint.
public record Slide(string Name, int Step, string Title, Sequence? Sequence = null, Grants? Grants = null,
    IReadOnlyList<string>? Points = null, Table? Table = null);

// Who talks to whom, in order. Lanes are the parties, left to right.
public record Sequence(IReadOnlyList<string> Lanes, IReadOnlyList<Message> Messages);

// A request (solid), an answer (dashed), or a note on one lane (From == To). Important ones are orange.
public record Message(int From, int To, string Text, bool Answer = false, bool Important = false);

// The two objects and where grants live. Tenants: my tenant, or mine and InGen's.
public record Grants(bool SecondTenant);

public record Table(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

public static class SlideDeck
{
    // The steps on the start page, in order. After the last step, Next goes back to the start page.
    public static readonly IReadOnlyList<(int Number, string Title)> Steps =
    [
        (1, "App registration and ID token"), (2, "Code flow and access token"), (3, "Calling my own API"),
        (4, "Refresh tokens across APIs"), (5, "On-Behalf-Of"), (6, "An app with no user"), (7, "Managed identity"),
        (8, "Cross-tenant with InGen"), (9, "The digest across tenants"), (10, "No secrets anywhere"),
    ];

    public static int? NextStep(int step) => Steps.Select(s => s.Number).Where(n => n > step).Cast<int?>().FirstOrDefault();

    public static readonly IReadOnlyList<Slide> All =
    [
        new("id-token", 1, "Signing in with an ID token",
            new(["Browser", "Kick-Ass Calendar", "Entra"],
            [
                new(0, 2, "GET /authorize  response_type=id_token  scope=openid profile email", Important: true),
                new(2, 2, "Damon signs in, and consents"),
                new(2, 0, "302 /callback#id_token=…", Answer: true),
                new(0, 1, "the id_token, in the fragment or a form post"),
                new(1, 1, "signature, aud, nonce: Damon is signed in"),
            ])),

        new("code-flow", 2, "The code flow",
            new(["Browser", "Kick-Ass Calendar", "Entra", "Graph"],
            [
                new(0, 2, "GET /authorize  response_type=code  code_challenge"),
                new(2, 2, "Damon signs in, consents to User.Read"),
                new(2, 0, "302 /callback?code=…", Answer: true),
                new(0, 1, "the code"),
                new(1, 2, "POST /token  code + code_verifier + client_secret", Important: true),
                new(2, 1, "id_token + access_token (aud: Graph)", Answer: true),
                new(1, 3, "GET /me/photo/$value  Bearer access_token"),
                new(3, 1, "the picture", Answer: true),
            ])),

        new("validation", 3, "How my API knows it's real",
            new(["Calendar API", "Entra"],
            [
                new(0, 1, "GET /.well-known/openid-configuration  (once)"),
                new(1, 0, "issuer, jwks_uri", Answer: true),
                new(0, 1, "GET jwks_uri  (once, then cached)"),
                new(1, 0, "the public keys, each with a kid", Answer: true),
                new(0, 0, "every request: key by kid, signature, iss, aud, nbf/exp", Important: true),
            ])),

        new("refresh", 4, "One refresh token, any API",
            new(["Kick-Ass Calendar", "Entra", "Calendar API", "Graph"],
            [
                new(0, 1, "POST /token  code  (scope: the API + offline_access)"),
                new(1, 0, "access_token (the API) + refresh_token", Answer: true),
                new(0, 2, "GET /calendar"),
                new(0, 1, "grant_type=refresh_token  scope=User.Read", Important: true),
                new(1, 0, "access_token (Graph) + a new refresh_token", Answer: true),
                new(0, 3, "GET /me/photo/$value"),
            ])),

        new("one-audience", 5, "One token, one audience",
            new(["Kick-Ass Calendar", "Entra", "Calendar API", "Graph"],
            [
                new(0, 1, "scope=api://kickassdev.se/calendar/KickAssCalendar.Read"),
                new(1, 0, "access_token  aud: the Calendar API", Answer: true, Important: true),
                new(0, 2, "GET /calendar  Bearer access_token"),
                new(2, 2, "aud is me, scp has KickAssCalendar.Read"),
                new(2, 3, "the same token, passed on"),
                new(3, 2, "401: not for Graph", Answer: true, Important: true),
            ])),

        new("on-behalf-of", 5, "On-Behalf-Of",
            new(["Kick-Ass Calendar", "Calendar API", "Entra", "Graph"],
            [
                new(0, 1, "GET /calendar  Bearer (aud: the Calendar API)"),
                new(1, 2, "grant_type=jwt-bearer  assertion=that token  the API's own secret", Important: true),
                new(2, 2, "has Damon let the API read his calendar?"),
                new(2, 1, "access_token  aud: Graph, still Damon", Answer: true),
                new(1, 3, "GET /me/calendarView"),
                new(3, 1, "Damon's day", Answer: true),
                new(1, 0, "Damon's day", Answer: true),
            ])),
        new("client-credentials", 6, "An app with no user",
            new(["Digest", "Entra", "Calendar API", "Graph"],
            [
                new(0, 1, "grant_type=client_credentials  scope=…/calendar/.default  the Digest's secret", Important: true),
                new(1, 0, "access_token  roles: KickAssCalendar.Read.All", Answer: true),
                new(0, 2, "GET /users/damon@kickassdev.se/calendar"),
                new(2, 2, "roles has KickAssCalendar.Read.All"),
                new(2, 1, "client_credentials, as the API itself  scope=graph/.default"),
                new(1, 2, "access_token  roles: Calendars.Read", Answer: true),
                new(2, 3, "GET /users/damon@kickassdev.se/calendarView"),
            ])),
        new("grants", 6, "Where grants live", Grants: new(SecondTenant: false)),

        new("managed-identity", 7, "A managed identity, in Azure",
            new(["Digest", "Azure platform", "Entra", "SQL, my API"],
            [
                new(0, 1, "GET $IDENTITY_ENDPOINT?resource=…  X-IDENTITY-HEADER", Important: true),
                new(1, 2, "the platform's own credentials for the managed identity"),
                new(2, 1, "access_token", Answer: true),
                new(1, 0, "access_token", Answer: true),
                new(0, 3, "connect, call, with the token"),
            ])),
        new("managed-identity-local", 7, "The same, on my laptop",
            new(["Digest", "Token service", "Google", "Entra"],
            [
                new(0, 1, "GET /msi/token?resource=…  X-IDENTITY-HEADER"),
                new(1, 2, "refresh_token"),
                new(2, 1, "a Google ID token", Answer: true),
                new(1, 3, "client_credentials  client_assertion=the Google ID token", Important: true),
                new(3, 3, "federated credential: issuer, subject, audience match"),
                new(3, 1, "the managed identity's access_token", Answer: true),
                new(1, 0, "what App Service would answer", Answer: true),
            ])),

        new("two-tenants", 8, "Another organization installs it", Grants: new(SecondTenant: true)),

        new("across-tenants", 9, "The digest in InGen's tenant",
            new(["Digest", "Azure platform", "Entra, InGen", "Graph"],
            [
                new(0, 1, "resource=api://AzureADTokenExchange"),
                new(1, 0, "the managed identity's token  (on my laptop: my Google login)", Answer: true),
                new(0, 2, "client_credentials  client_id=the Digest  client_assertion=that token", Important: true),
                new(2, 2, "the Digest trusts my managed identity; Nedry granted Calendars.Read"),
                new(2, 0, "access_token  tid: InGen  roles: Calendars.Read", Answer: true),
                new(0, 3, "GET /users/john.hammond@ingendino.se/calendarView"),
            ])),

        new("no-secrets", 10, "No secrets anywhere",
            new(["Kick-Ass Calendar", "Azure platform", "Entra"],
            [
                new(0, 1, "resource=api://AzureADTokenExchange"),
                new(1, 0, "the web's managed identity's token", Answer: true),
                new(0, 2, "POST /token  code + client_assertion=that token", Important: true),
                new(2, 2, "Kick-Ass Calendar trusts kickass-calendar-web"),
                new(2, 0, "id_token + access_token", Answer: true),
            ])),
    ];

    public static Slide? Find(string name) => All.FirstOrDefault(slide => slide.Name == name);

    public static IReadOnlyList<Slide> ForStep(int step) => All.Where(slide => slide.Step == step).ToList();
}

// SVG's <text> is a Razor keyword inside code blocks, so it's written out here.
public static class Svg
{
    public static Microsoft.AspNetCore.Html.IHtmlContent Text(double x, double y, string className, string content, string anchor = "start") =>
        new Microsoft.AspNetCore.Html.HtmlString(
            $"<text class=\"{className}\" x=\"{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" y=\"{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" text-anchor=\"{anchor}\">{System.Net.WebUtility.HtmlEncode(content)}</text>");
}
