using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Who the digest is to Entra: its own app registration, and a secret from config. Nobody signs in.
builder.Services.AddSingleton<TokenCredential>(new ClientSecretCredential(
    builder.Configuration["Entra:TenantId"], 
    builder.Configuration["Entra:ClientId"], 
    builder.Configuration["Entra:ClientSecret"])
);

// Who wants a digest. A SQL login, with a password.
builder.Services.AddScoped(_ => new SqlConnection(new SqlConnectionStringBuilder
{
    DataSource = "kickassdev-calendar.database.windows.net",
    InitialCatalog = "kickass-calendar",
    UserID = "digest",
    Password = builder.Configuration["Digest:SqlPassword"],
}.ConnectionString));

builder.Services.AddHttpClient("calendar-api", client => client.BaseAddress = new Uri("https+http://calendar-api"));

var app = builder.Build();

// The nightly job. Aspire's Send digest command runs it now, instead of waiting for the night.
app.MapPost("/send", Digest.SendAsync);
app.MapDefaultEndpoints();

app.Run();
