using System.Globalization;
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

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapProxy();
app.MapDefaultEndpoints();

app.Run();
