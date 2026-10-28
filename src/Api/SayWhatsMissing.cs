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
