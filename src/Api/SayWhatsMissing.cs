using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Microsoft.Extensions.DependencyInjection;

// A plain 403 has an empty body. 401 is "who are you?", 403 is "I know who you are, and no". This says what's missing.
public static class SayWhatsMissingExtensions
{
    // The policies themselves are in Program.cs, where they're shown. Their names are what the 403 says.
    public static IServiceCollection AddSayWhatsMissing(this IServiceCollection services) =>
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, SayWhatsMissing>();

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

            // Only what failed: a claim by what it is, not its values. A tid list is my customers, and stays out of it.
            var missing = (authorizeResult.AuthorizationFailure?.FailedRequirements ?? []).Select(requirement => requirement switch
            {
                ClaimsAuthorizationRequirement { ClaimType: "tid" } => "a tid that's on the list",
                ClaimsAuthorizationRequirement { ClaimType: "roles" } => AuthPolicies.ReadAllCalendars,
                _ => AuthPolicies.ReadCalendar,
            }).Distinct();
            await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Authenticated, but not authorized",
                detail: $"This needs {string.Join(" and ", missing)}.").ExecuteAsync(context);
        }
    }
}
