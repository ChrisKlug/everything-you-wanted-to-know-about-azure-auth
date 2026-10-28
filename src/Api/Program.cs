using Microsoft.IdentityModel.Logging;

var builder = WebApplication.CreateBuilder(args);

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
        options.Authority = $"https://login.microsoftonline.com/{builder.Configuration["Entra:TenantId"]}/v2.0";
        options.Audience = builder.Configuration["Entra:ClientId"];
        options.MapInboundClaims = false;
    });
builder.Services.AddCalendarAuthorization();

builder.Services.AddHttpClient("graph", client => client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"));

var app = builder.Build();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Who the API thinks is calling, straight from the token.
app.MapGet("/me", (HttpContext context) => context.User.Claims.Select(claim => new { claim.Type, claim.Value }))
    .RequireAuthorization();

// The user's own day: a user, through an app.
app.MapGet("/calendar", CalendarEndpoints.GetCalendarAsync).RequireAuthorization(CalendarAuthorization.Scope);
// Anyone's day, for the nightly digest. Nobody is signed in, so only apps an admin gave the role get in.
app.MapGet("/users/{id}/calendar", CalendarEndpoints.GetUserCalendarAsync).RequireAuthorization(CalendarAuthorization.AppRole);

app.MapDefaultEndpoints();

app.Run();
