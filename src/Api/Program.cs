using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Validators;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSayWhatsMissing();

builder.AddServiceDefaults();

// Token validation errors hide the values (they're personal data). Show them, but only in development.
if (builder.Environment.IsDevelopment())
{
    IdentityModelEventSource.ShowPII = true;
    AppContext.SetSwitch("Switch.Microsoft.IdentityModel.DoNotScrubExceptions", true);
}

builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://login.microsoftonline.com/organizations/v2.0";
        options.Audience = builder.Configuration["Entra:ClientId"];
        options.MapInboundClaims = false;
        options.TokenValidationParameters.IssuerValidator = AadIssuerValidator.GetAadIssuerValidator(options.Authority).Validate;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(
        // A user, through an app: the delegated scope. scp is one string, space separated.
        AuthPolicies.ReadCalendar,
        policy => policy.RequireAssertion(context =>
            context.User.FindFirst("scp")?.Value.Split(' ').Contains("KickAssCalendar.Read") == true
        )
    )
    .AddPolicy(
        // An app on its own, nobody signed in: the app role an admin granted it. roles is one claim per role.
        AuthPolicies.ReadAllCalendars, policy => policy.RequireClaim("roles", "KickAssCalendar.Read.All")
    );

builder.Services.AddHttpClient("graph", client => client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"));

var app = builder.Build();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Who the API thinks is calling, straight from the token.
app.MapGet("/me", (HttpContext context) =>
        context.User.Claims.Select(claim => new { claim.Type, claim.Value })
    )
    .RequireAuthorization();

// Only customers. In real life the list is the sign-up database.
var customers = builder.Configuration["Customers:TenantIds"]!.Split(',');

// The user's own day: a user, through an app.
app.MapGet("/calendar", CalendarEndpoints.GetCalendarAsync)
    .RequireAuthorization(AuthPolicies.ReadCalendar)
    .RequireAuthorization(policy => policy.RequireClaim("tid", customers));

// Anyone's day, for the nightly digest. Nobody is signed in, so only apps an admin gave the role get in.
app.MapGet("/users/{id}/calendar", CalendarEndpoints.GetUserCalendarAsync)
    .RequireAuthorization(AuthPolicies.ReadAllCalendars)
    .RequireAuthorization(policy => policy.RequireClaim("tid", customers));

app.MapDefaultEndpoints();

app.Run();
