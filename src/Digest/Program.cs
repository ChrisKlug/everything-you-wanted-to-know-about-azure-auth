using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Who the digest is to Entra: its own app registration, in whichever tenant asks. No secret: the managed identity's
// token is its credential, and the registration's federated credential trusts it.
var managedIdentity = new ManagedIdentityCredential(
    ManagedIdentityId.FromUserAssignedClientId(builder.Configuration["Entra:ManagedIdentityClientId"])
);

builder.Services.AddSingleton<TokenCredential>(new ClientAssertionCredential(
    builder.Configuration["Entra:TenantId"],
    builder.Configuration["Entra:ClientId"],
    async cancellationToken => (await managedIdentity.GetTokenAsync(new(["api://AzureADTokenExchange/.default"]), cancellationToken)).Token,
    new() { AdditionallyAllowedTenants = { "*" } })
);

// Who wants a digest. No password: the digest's managed identity.
builder.Services.AddScoped(_ => new SqlConnection(new SqlConnectionStringBuilder
{
    DataSource = "kickassdev-calendar.database.windows.net",
    InitialCatalog = "kickass-calendar",
    Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity,
    UserID = builder.Configuration["Entra:ManagedIdentityClientId"],
}.ConnectionString));


builder.Services.AddHttpClient("calendar-api", client => client.BaseAddress = new Uri("https+http://calendar-api"));

var app = builder.Build();

// The nightly job. Aspire's Send digest command runs it now, instead of waiting for the night.
app.MapPost("/send", Digest.SendAsync);
app.MapDefaultEndpoints();

app.Run();
