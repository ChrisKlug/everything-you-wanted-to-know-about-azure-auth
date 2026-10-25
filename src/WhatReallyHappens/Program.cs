using System.Globalization;
using Azure.Core;
using Azure.Identity;
using Zkd.Demo.Azure.Auth.WhatReallyHappens;
using Zkd.Demo.Azure.Auth.WhatReallyHappens.Proxy;

var builder = WebApplication.CreateBuilder(args);

// The diagrams are SVG, and SVG wants 12.5, not the 12,5 a Swedish laptop would write.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

builder.AddServiceDefaults();

builder.Services.AddRazorPages();
builder.Services.Configure<EntraOptions>(builder.Configuration.GetSection("Entra"));
builder.Services.Configure<ProxyOptions>(builder.Configuration.GetSection("Proxy"));
builder.Services.AddHttpClient(ProxyEndpoints.HttpClientName);
// The web's managed identity, for step 10. Picked by resource ID, like the web does. Only asked when a step uses it.
builder.Services.AddKeyedSingleton<TokenCredential>("web", (_, _) => new ManagedIdentityCredential(
    ManagedIdentityId.FromUserAssignedResourceId(new ResourceIdentifier(builder.Configuration["Entra:ManagedIdentityResourceId"]
        ?? "/subscriptions/none/resourceGroups/none/providers/Microsoft.ManagedIdentity/userAssignedIdentities/none"))));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapProxy();
app.MapDefaultEndpoints();

app.Run();
