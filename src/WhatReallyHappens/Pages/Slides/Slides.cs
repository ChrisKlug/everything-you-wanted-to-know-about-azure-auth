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
public record Grants(bool SecondTenant, bool Detailed);

public record Table(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

public static class SlideDeck
{
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
            ]),
            Points:
            [
                "The ID token says who the user is, for my app. That's all it's for.",
                "It came back through the browser. Fine for who, not for anything that opens doors.",
                "Every library starts at the openid-configuration: where everything is.",
            ]),
        new("two-objects", 1, "Two objects", Grants: new(SecondTenant: false, Detailed: false),
            Points:
            [
                "The app registration is the store listing: one, in the publisher's tenant.",
                "The enterprise app is the installed copy: one per tenant that uses it. Consent lives here.",
                "API permissions on the registration are what the app declares. The enterprise app has what it got.",
            ]),

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
            ]),
            Points:
            [
                "Only the code goes through the browser. It's useless without the secret and the verifier.",
                "ID token: who Damon is, for my app. Access token: what my app may call, for Graph.",
                "PKCE: whoever started the sign-in is the one who finishes it.",
            ]),

        new("one-audience", 3, "One token, one audience",
            new(["Kick-Ass Calendar", "Entra", "Calendar API", "Graph"],
            [
                new(0, 1, "scope=https://kickassdev.se/api/KickAssCalendar.Read"),
                new(1, 0, "access_token  aud: the Calendar API", Answer: true, Important: true),
                new(0, 2, "GET /calendar  Bearer access_token"),
                new(2, 2, "aud is me, scp has KickAssCalendar.Read"),
                new(2, 3, "the same token, passed on"),
                new(3, 2, "401: not for Graph", Answer: true, Important: true),
            ]),
            Points:
            [
                "A token is for one API. Ask for two resources and the first one wins, without a word.",
                "The API a token is for decides its version: requestedAccessTokenVersion, in the manifest.",
                "knownClientApplications: the API vouches for its own front end, so one consent screen.",
            ]),

        new("validation", 4, "How my API knows it's real",
            new(["Calendar API", "Entra"],
            [
                new(0, 1, "GET /.well-known/openid-configuration  (once)"),
                new(1, 0, "issuer, jwks_uri", Answer: true),
                new(0, 1, "GET jwks_uri  (once, then cached)"),
                new(1, 0, "the public keys, each with a kid", Answer: true),
                new(0, 0, "every request: key by kid, signature, iss, aud, nbf/exp", Important: true),
            ]),
            Points:
            [
                "A valid signature only says Entra issued it. A token for someone else's API is just as valid.",
                "That's why iss and aud are checks of their own.",
                "AddJwtBearer() does all of it, and caches the keys.",
            ]),

        new("refresh", 5, "One refresh token, any API",
            new(["Kick-Ass Calendar", "Entra", "Calendar API", "Graph"],
            [
                new(0, 1, "POST /token  code  (scope: the API + offline_access)"),
                new(1, 0, "access_token (the API) + refresh_token", Answer: true),
                new(0, 2, "GET /calendar"),
                new(0, 1, "grant_type=refresh_token  scope=User.Read", Important: true),
                new(1, 0, "access_token (Graph) + a new refresh_token", Answer: true),
                new(0, 3, "GET /me/photo/$value"),
            ]),
            Points:
            [
                "One refresh token gets a token for any API the user has consented to. That's Entra, not plain OAuth.",
                "Anything new needs the user in front of the screen (AADSTS65001).",
                "Use the new refresh token each time. Let a library keep them.",
            ]),

        new("on-behalf-of", 6, "On-Behalf-Of",
            new(["Kick-Ass Calendar", "Calendar API", "Entra", "Graph"],
            [
                new(0, 1, "GET /calendar  Bearer (aud: the Calendar API)"),
                new(1, 2, "grant_type=jwt-bearer  assertion=that token  the API's own secret", Important: true),
                new(2, 2, "has Damon let the API read his calendar?"),
                new(2, 1, "access_token  aud: Graph, still Damon", Answer: true),
                new(1, 3, "GET /me/calendarView"),
                new(3, 1, "Damon's day", Answer: true),
                new(1, 0, "Damon's day", Answer: true),
            ]),
            Points:
            [
                "The API is the one asking, so the API needs the consent. AADSTS65001 says so, by name.",
                ".default and knownClientApplications put the API's needs on the user's consent screen.",
                "A new permission later: an admin grants it first, or the web sends the user back with prompt=consent.",
            ]),
        new("who-consented", 6, "Who consented to what",
            Table: new(["Who may", "Call", "As Damon, for", "Consented"],
            [
                ["Kick-Ass Calendar", "Graph", "openid profile email User.Read Calendars.Read", "Steps 2 and 6"],
                ["Kick-Ass Calendar", "Calendar API", "KickAssCalendar.Read", "Step 3"],
                ["Calendar API", "Graph", "Calendars.Read", "Step 6, with prompt=consent"],
            ]),
            Points: [".default: what the portal says, not what you ask for at sign-in."]),

        new("client-credentials", 7, "An app with no user",
            new(["Digest", "Entra", "Calendar API", "Graph"],
            [
                new(0, 1, "grant_type=client_credentials  scope=…/api/.default  the Digest's secret", Important: true),
                new(1, 0, "access_token  roles: KickAssCalendar.Read.All", Answer: true),
                new(0, 2, "GET /users/damon@kickassdev.se/calendar"),
                new(2, 2, "roles has KickAssCalendar.Read.All"),
                new(2, 1, "client_credentials, as the API itself  scope=graph/.default"),
                new(1, 2, "access_token  roles: Calendars.Read", Answer: true),
                new(2, 3, "GET /users/damon@kickassdev.se/calendarView"),
            ]),
            Points:
            [
                "Scopes are for a user through an app. Roles are for an app on its own.",
                "Authenticated isn't authorized: any app in the tenant can get a token for my API.",
                ".default is the only scope there is. An admin grants, nobody consents.",
            ]),
        new("grants", 7, "Where grants live", Grants: new(SecondTenant: false, Detailed: true),
            Points:
            [
                "Two objects, same appId, different id.",
                "Delegated: oauth2PermissionGrants. Application: appRoleAssignments. Both on the enterprise app.",
                "The registration only shows them. \"Granted for Kick-Ass Development\" is the enterprise app's.",
            ]),

        new("managed-identity", 8, "A managed identity, in Azure",
            new(["Digest", "Azure platform", "Entra", "SQL, my API"],
            [
                new(0, 1, "GET $IDENTITY_ENDPOINT?resource=…  X-IDENTITY-HEADER", Important: true),
                new(1, 2, "the platform's own credentials for the managed identity"),
                new(2, 1, "access_token", Answer: true),
                new(1, 0, "access_token", Answer: true),
                new(0, 3, "connect, call, with the token"),
            ]),
            Points:
            [
                "To the SDK a managed identity is two variables and an HTTP call.",
                "No secret anywhere in the app. The platform proves who it is.",
            ]),
        new("managed-identity-local", 8, "The same, on my laptop",
            new(["Digest", "Token service", "Google", "Entra"],
            [
                new(0, 1, "GET /msi/token?resource=…  X-IDENTITY-HEADER"),
                new(1, 2, "refresh_token"),
                new(2, 1, "a Google ID token", Answer: true),
                new(1, 3, "client_credentials  client_assertion=the Google ID token", Important: true),
                new(3, 3, "federated credential: issuer, subject, audience match"),
                new(3, 1, "the managed identity's access_token", Answer: true),
                new(1, 0, "what App Service would answer", Answer: true),
            ]),
            Points:
            [
                "Entra trusts whatever issuer you tell it to, as long as issuer, subject and audience match.",
                "Powerful, and why a sloppy federated credential is dangerous.",
                "A demo trick. Locally you'd normally just be yourself, with DefaultAzureCredential.",
            ]),

        new("two-tenants", 9, "Another organization installs it", Grants: new(SecondTenant: true, Detailed: true),
            Points:
            [
                "One registration, an enterprise app in every tenant that uses it: same Application ID, different Object ID.",
                "Nedry's admin consent is AllPrincipals: everyone at InGen.",
                "Entra says who they are. Whether their organization is a customer is my job: the tid list.",
            ]),

        new("across-tenants", 10, "The digest in InGen's tenant",
            new(["Digest", "Azure platform", "Entra, InGen", "Graph"],
            [
                new(0, 1, "resource=api://AzureADTokenExchange"),
                new(1, 0, "the managed identity's token  (on my laptop: my Google login)", Answer: true),
                new(0, 2, "client_credentials  client_id=the Digest  client_assertion=that token", Important: true),
                new(2, 2, "the Digest trusts my managed identity; Nedry granted Calendars.Read"),
                new(2, 0, "access_token  tid: InGen  roles: Calendars.Read", Answer: true),
                new(0, 3, "GET /users/john.hammond@ingendino.se/calendarView"),
            ]),
            Points:
            [
                "A managed identity only exists in my tenant. The Digest registration is who the digest is in InGen.",
                "The first token proves who the digest is. The second is who it is in InGen.",
                "Nedry granted it on purpose, for the digest only. He can take it away again.",
            ]),

        new("no-secrets", 11, "No secrets anywhere",
            new(["Kick-Ass Calendar", "Azure platform", "Entra"],
            [
                new(0, 1, "resource=api://AzureADTokenExchange"),
                new(1, 0, "the web's managed identity's token", Answer: true),
                new(0, 2, "POST /token  code + client_assertion=that token", Important: true),
                new(2, 2, "Kick-Ass Calendar trusts kickass-calendar-web"),
                new(2, 0, "id_token + access_token", Answer: true),
            ]),
            Points:
            [
                "The registration is still who the app is. Only the credential changed.",
                "User-assigned: it exists before the app, survives redeploys, and slots can share it.",
                "Web, API or worker: no secrets.",
            ]),
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
