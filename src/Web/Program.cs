using Microsoft.IdentityModel.Validators;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorPages();
builder.Services.AddHttpClient("graph", client => client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"));
builder.Services.AddHttpClient("calendar-api", client => client.BaseAddress = new Uri("https+http://calendar-api"));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "Cookies";
        options.DefaultChallengeScheme = "OpenIdConnect";
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        options.Authority = "https://login.microsoftonline.com/organizations/v2.0";
        options.ClientId = builder.Configuration["Entra:ClientId"];
        options.ClientSecret = builder.Configuration["Entra:ClientSecret"];
        options.ResponseType = "code";
        options.Scope.Add("api://kickassdev.se/calendar/.default");
        options.Scope.Add("offline_access");
        options.SaveTokens = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.IssuerValidator = AadIssuerValidator.GetAadIssuerValidator(options.Authority).Validate;
        options.Events.OnTokenValidated = context =>
        {
            var tenant = context.Principal!.FindFirst("tid")!.Value;
            if (!builder.Configuration["Customers:TenantIds"]!.Split(',').Contains(tenant))
            {
                context.Response.Redirect($"/magic-word?tenant={tenant}");
                context.HandleResponse();
            }
            return Task.CompletedTask;
        };
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapDefaultEndpoints();

app.Run();
