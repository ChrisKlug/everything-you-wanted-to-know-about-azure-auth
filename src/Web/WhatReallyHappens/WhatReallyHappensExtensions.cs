using Microsoft.AspNetCore.Mvc.RazorPages;
using Zkd.Demo.Azure.Auth.Web;
using Zkd.Demo.Azure.Auth.Web.Proxy;

namespace Microsoft.Extensions.Hosting;

// Everything the /what-really-happens page needs, kept out of Program.cs. Program.cs is the real app.
public static class WhatReallyHappensExtensions
{
    public static TBuilder AddWhatReallyHappens<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.Configure<EntraOptions>(builder.Configuration.GetSection("Entra"));
        builder.Services.Configure<ProxyOptions>(builder.Configuration.GetSection("Proxy"));

        builder.Services.AddHttpClient(ProxyEndpoints.HttpClientName);

        // Sign-in comes back to the same page, so it can pick up where it left off. PostConfigure, because AddRazorPages()
        // in Program.cs runs its own setup after anything configured here.
        builder.Services.PostConfigure<RazorPagesOptions>(options =>
            options.Conventions.AddPageRoute("/WhatReallyHappens/Index", "what-really-happens/callback"));

        return builder;
    }

    public static WebApplication MapWhatReallyHappens(this WebApplication app)
    {
        app.MapProxy();
        return app;
    }
}
