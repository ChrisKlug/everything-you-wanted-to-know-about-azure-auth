using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Microsoft.Extensions.DependencyInjection;

// A valid token says who's calling. These say what they may do. The policy names are what a 403 says is missing.
public static class CalendarAuthorization
{
    // A user, through an app: the delegated scope. scp is one string, space separated.
    public const string Scope = "the scope KickAssCalendar.Read";

    // An app on its own, nobody signed in: the app role an admin granted it. roles is one claim per role.
    public const string AppRole = "the app role KickAssCalendar.Read.All";

    public static IServiceCollection AddCalendarAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Scope, policy => policy.RequireAssertion(context =>
                context.User.FindFirst("scp")?.Value.Split(' ').Contains("KickAssCalendar.Read") == true))
            .AddPolicy(AppRole, policy => policy.RequireClaim("roles", "KickAssCalendar.Read.All"));
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, SayWhatsMissing>();
        return services;
    }

    // A plain 403 has an empty body. 401 is "who are you?", 403 is "I know who you are, and no". Say what's missing.
    private class SayWhatsMissing : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();

        public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            if (!authorizeResult.Forbidden)
            {
                await defaultHandler.HandleAsync(next, context, policy, authorizeResult);
                return;
            }

            // Named policies say it with their name. Inline ones (a list of tenants) by the claim they need, not its values.
            var metadata = context.GetEndpoint()?.Metadata;
            var named = metadata?.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).OfType<string>() ?? [];
            var inline = metadata?.GetOrderedMetadata<AuthorizationPolicy>().SelectMany(p => p.Requirements)
                .OfType<ClaimsAuthorizationRequirement>().Select(claim => $"a {claim.ClaimType} that's on the list") ?? [];
            var missing = inline.Concat(named);
            await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Authenticated, but not authorized",
                detail: $"This needs {string.Join(" and ", missing)}.").ExecuteAsync(context);
        }
    }
}
