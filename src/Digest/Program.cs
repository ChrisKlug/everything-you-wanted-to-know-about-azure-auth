using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Who the digest is to Entra: its managed identity. No secret, the platform vouches for it.
builder.Services.AddSingleton<TokenCredential>(new ManagedIdentityCredential(
    ManagedIdentityId.FromUserAssignedClientId(builder.Configuration["Entra:ManagedIdentityClientId"]))
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
