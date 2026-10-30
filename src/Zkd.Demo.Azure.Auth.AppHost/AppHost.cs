var builder = DistributedApplication.CreateBuilder(args);

var tenantId = builder.AddParameter("kickass-tenant-id")
    .WithDescription("The **Directory (tenant) ID** of the Kick-Ass Development tenant.", enableMarkdown: true);
var webClientId = builder.AddParameter("web-client-id")
    .WithDescription("The **Application (client) ID** of the *Kick-Ass Calendar* app registration.", enableMarkdown: true);
var webClientSecret = builder.AddParameter("web-client-secret", secret: true)
    .WithDescription("A **client secret** created under *Certificates & secrets* on the *Kick-Ass Calendar* app registration.", enableMarkdown: true)
    .WithHidden();
// Has a default, so the API runs before it's registered.
var calendarApiClientId = builder.AddParameter("calendar-api-client-id", new PlaceholderDefault("not-registered-yet"))
    .WithDescription("The **Application (client) ID** of the *Kick-Ass Calendar API* app registration.", enableMarkdown: true);
// The API's own credentials, for On-Behalf-Of.
var calendarApiClientSecret = builder.AddParameter("calendar-api-client-secret", new PlaceholderDefault("not-created-yet"), secret: true)
    .WithDescription("A **client secret** created under *Certificates & secrets* on the *Kick-Ass Calendar API* app registration.", enableMarkdown: true);
// The nightly digest, an app with no user.
var digestClientId = builder.AddParameter("digest-client-id", new PlaceholderDefault("not-registered-yet"))
    .WithDescription("The **Application (client) ID** of the *Kick-Ass Calendar Digest* app registration.", enableMarkdown: true);
var digestClientSecret = builder.AddParameter("digest-client-secret", new PlaceholderDefault("not-created-yet"), secret: true)
    .WithDescription("A **client secret** created under *Certificates & secrets* on the *Kick-Ass Calendar Digest* app registration.", enableMarkdown: true);
var digestSqlPassword = builder.AddParameter("digest-sql-password", new PlaceholderDefault("not-created-yet"), secret: true)
    .WithDescription("The password of the `digest` SQL login in the *kickass-calendar* database.", enableMarkdown: true)
    .WithHidden();
var digestIdentityClientId = builder.AddParameter("digest-identity-client-id", new PlaceholderDefault("not-created-yet"))
    .WithDescription("The **Client ID** of the *kickass-calendar-digest* managed identity.", enableMarkdown: true);
// No default: on a new machine the dashboard asks for it.
var googleClientSecret = builder.AddParameter("google-client-secret", secret: true)
    .WithDescription("The **client secret** of the Google OAuth client the token service signs in with. It's in 1Password.", enableMarkdown: true);

// In real life this is the sign-up database.
var customerTenants = builder.AddParameter("customer-tenants", new PlaceholderDefault(""))
    .WithDescription("The **tenant IDs** of the organizations that are customers, comma separated.", enableMarkdown: true);

var calendarApi = builder.AddProject<Projects.Api>("calendar-api")
    .WithEnvironment("Customers__TenantIds", customerTenants)
    .WithEnvironment("Entra__TenantId", tenantId)
    .WithEnvironment("Entra__ClientId", calendarApiClientId)
    .WithEnvironment("Entra__ClientSecret", calendarApiClientSecret);

// Catches the digest's mail, so nothing leaves the laptop. Docker has to be running.
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.31.4")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp")
    .WithUrlForEndpoint("http", url => url.DisplayText = "Inbox")
    .WithUrlForEndpoint("smtp", url => url.DisplayLocation = UrlDisplayLocation.DetailsOnly);

// Plays the Azure platform: the managed identity endpoint App Service gives an app. The header is the platform's
// secret handshake, new on every run, like in Azure.
var identityHeader = Guid.NewGuid().ToString("N");
var tokenService = builder.AddProject<Projects.TokenService>("token-service")
    .WithEnvironment("IDENTITY_HEADER", identityHeader)
    .WithEnvironment("Entra__TenantId", tenantId)
    .WithEnvironment("Google__ClientSecret", googleClientSecret)
    .WithUrls(context =>
    {
        foreach (var url in context.Urls.ToList())
        {
            switch (url.Endpoint?.EndpointName)
            {
                case "https":
                    url.DisplayText = "Google login";
                    url.Url = $"{url.Url.TrimEnd('/')}/google/login";
                    break;
                case "http":
                    url.DisplayLocation = UrlDisplayLocation.DetailsOnly;
                    break;
            }
        }
    });

// The web and the API pick their managed identity by resource ID, which is known before the identity exists.
const string identities = "/subscriptions/bec02fc1-d197-4e23-8d83-61b053b4f51d/resourceGroups/kickass-demo/providers/Microsoft.ManagedIdentity/userAssignedIdentities";
calendarApi
    .WithEnvironment("IDENTITY_ENDPOINT", ReferenceExpression.Create($"{tokenService.GetEndpoint("http")}/msi/token"))
    .WithEnvironment("IDENTITY_HEADER", identityHeader)
    .WithEnvironment("Entra__ManagedIdentityResourceId", $"{identities}/kickass-calendar-api");

builder.AddProject<Projects.Digest>("digest")
    .WithReference(calendarApi)
    .WithEnvironment("Smtp__Endpoint", mailpit.GetEndpoint("smtp"))
    .WithEnvironment("Digest__SqlPassword", digestSqlPassword)
    .WithEnvironment("Entra__TenantId", tenantId)
    .WithEnvironment("Entra__ClientId", digestClientId)
    .WithEnvironment("Entra__ClientSecret", digestClientSecret)
    // What App Service gives an app with a managed identity.
    .WithEnvironment("IDENTITY_ENDPOINT", ReferenceExpression.Create($"{tokenService.GetEndpoint("http")}/msi/token"))
    .WithEnvironment("IDENTITY_HEADER", identityHeader)
    .WithEnvironment("Entra__ManagedIdentityClientId", digestIdentityClientId)
    .WithHttpCommand("/send", "Send digest", commandOptions: new()
    {
        Method = HttpMethod.Post,
        Description = "Runs the nightly digest now.",
        IconName = "Mail",
    });

builder.AddProject<Projects.Web>("web")
    .WithReference(calendarApi)
    // The digest's admin consent page asks for the Digest registration by client ID.
    .WithEnvironment("Digest__ClientId", digestClientId)
    .WithEnvironment("Customers__TenantIds", customerTenants)
    .WithEnvironment("Entra__TenantId", tenantId)
    .WithEnvironment("Entra__ClientId", webClientId)
    .WithEnvironment("Entra__ClientSecret", webClientSecret)
    .WithEnvironment("IDENTITY_ENDPOINT", ReferenceExpression.Create($"{tokenService.GetEndpoint("http")}/msi/token"))
    .WithEnvironment("IDENTITY_HEADER", identityHeader)
    .WithEnvironment("Entra__ManagedIdentityResourceId", $"{identities}/kickass-calendar-web")
    .WithUrls(context =>
    {
        foreach (var url in context.Urls.ToList())
        {
            switch (url.Endpoint?.EndpointName)
            {
                case "https":
                    url.DisplayText = "Home";
                    break;
                case "http":
                    url.DisplayLocation = UrlDisplayLocation.DetailsOnly;
                    break;
            }
        }
    });

// Every request the web makes, by hand, one page per step. It signs in as the web: same registration, same secret.
builder.AddProject<Projects.WhatReallyHappens>("what-really-happens")
    .WithReference(calendarApi)
    .WithEnvironment("Entra__TenantId", tenantId)
    .WithEnvironment("Entra__ClientId", webClientId)
    .WithEnvironment("Entra__ClientSecret", webClientSecret)
    .WithEnvironment("CalendarApi__ClientId", calendarApiClientId)
    .WithEnvironment("CalendarApi__ClientSecret", calendarApiClientSecret)
    .WithEnvironment("Digest__ClientId", digestClientId)
    .WithEnvironment("Digest__ClientSecret", digestClientSecret)
    .WithUrls(context =>
    {
        foreach (var url in context.Urls.ToList())
        {
            switch (url.Endpoint?.EndpointName)
            {
                case "https":
                    url.DisplayText = "What really happens";
                    break;
                case "http":
                    url.DisplayLocation = UrlDisplayLocation.DetailsOnly;
                    break;
            }
        }
    });

builder.Build().Run();

// A fixed value, used until configuration (user secrets, or the dashboard's Set command) has one.
class PlaceholderDefault(string value) : ParameterDefault
{
    public override string GetDefaultValue() => value;

    public override void WriteToManifest(Aspire.Hosting.Publishing.ManifestPublishingContext context) => context.Writer.WriteString("value", value);
}
