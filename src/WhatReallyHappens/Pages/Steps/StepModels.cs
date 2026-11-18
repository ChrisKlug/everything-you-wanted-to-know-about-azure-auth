using Microsoft.AspNetCore.Mvc.RazorPages;
using Zkd.Demo.Azure.Auth.WhatReallyHappens.Pages.Shared;

namespace Zkd.Demo.Azure.Auth.WhatReallyHappens.Pages.Steps;

// What each step's page offers: the sign-in's choices and the calls. Only what the step's story needs.
public abstract class StepModel(IConfiguration configuration) : PageModel
{
    protected IConfiguration Configuration => configuration;

    public virtual ChallengeOptions? Login => null;
    public virtual ApiCallOptions? Api => null;
}

public class Step2Model(IConfiguration configuration) : StepModel(configuration)
{
    // The code flow, with PKCE. Tick User.Read first to see Entra quietly drop the API's scope.
    public override ChallengeOptions Login => new(
        [("profile", true), ("email", true), ("User.Read", false), (Apis.Scope, true)], ["code"], [], []);

    public override ApiCallOptions Api => new(
        [("Kick-Ass Calendar API", [Apis.Calendar(Configuration, "/me")])],
        ["id_token", "access_token"]);
}

public class Step3Model(IConfiguration configuration) : StepModel(configuration)
{
    // offline_access is what this step ticks: "and give me a refresh token".
    public override ChallengeOptions Login => new(
        [("profile", true), ("email", true), (Apis.Scope, true), ("offline_access", false)], ["code"], [], [], Pkce: false);

    public override ApiCallOptions Api => new(
        [("Microsoft Graph", [Apis.Graph("/me/photo/$value")])],
        ["access_token", "refreshed_token"]);
}

public class Step4Model(IConfiguration configuration) : StepModel(configuration)
{
    // The trap is the order (the API first), then .default, then prompt=consent.
    public override ChallengeOptions Login => new(
        [("profile", true), ("email", true), (Apis.Scope, true), ("Calendars.Read", false), (Apis.DefaultScope, false)],
        ["code"], [], ["", "consent"], Pkce: false);

    public override ApiCallOptions Api => new(
        [("Microsoft Graph", [Apis.Graph("/me/events?$select=subject,start,end&$top=5")])],
        ["access_token", "obo_token"]);
}

public class Step5Model(IConfiguration configuration) : StepModel(configuration)
{
    public override ApiCallOptions Api => new(
        [("Kick-Ass Calendar API", [Apis.Calendar(Configuration, "/users/damon@kickassdev.se/calendar"), Apis.Calendar(Configuration, "/me")])],
        ["access_token"]);
}

public class Step6Model(IConfiguration configuration) : StepModel(configuration);

public class Step7Model(IConfiguration configuration) : StepModel(configuration)
{
    // Any organization's users: the authority is organizations, not my tenant.
    public override ChallengeOptions Login => new(
        [("profile", true), ("email", true), (Apis.DefaultScope, true)], ["code"], [], ["select_account", ""], Pkce: false);

    public override ApiCallOptions Api => new(
        [("Kick-Ass Calendar API", [Apis.Calendar(Configuration, "/calendar"), Apis.Calendar(Configuration, "/me")])],
        ["access_token"]);
}
