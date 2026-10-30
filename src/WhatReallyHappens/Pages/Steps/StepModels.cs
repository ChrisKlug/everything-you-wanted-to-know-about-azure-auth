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

public class Step1Model(IConfiguration configuration) : StepModel(configuration)
{
    // An ID token straight back through the browser. response_mode is the point: fragment, form_post, and query refused.
    public override ChallengeOptions Login => new(
        [("profile", true), ("email", true)], ["id_token"], ["fragment", "form_post", "query"], []);
}

public class Step2Model(IConfiguration configuration) : StepModel(configuration)
{
    // Starts where step 1 left off: an ID token, nothing else. User.Read and code are what this step changes.
    public override ChallengeOptions Login => new(
        [("profile", true), ("email", true), ("User.Read", false)], ["id_token", "code"], [], []);

    public override ApiCallOptions Api => new(
        [("Microsoft Graph", [Apis.Graph("/me/photo/$value")])],
        ["id_token", "access_token"]);
}

public class Step3Model(IConfiguration configuration) : StepModel(configuration)
{
    // A token for my API. Tick User.Read first to see Entra quietly drop the API's scope.
    public override ChallengeOptions Login => new(
        [("profile", true), ("email", true), ("User.Read", false), (Apis.Scope, true)], ["code"], [], []);

    public override ApiCallOptions Api => new(
        [
            ("Kick-Ass Calendar API", [Apis.Calendar(Configuration, "/me"), Apis.Calendar(Configuration, "/calendar")]),
            ("Microsoft Graph", [Apis.Graph("/me")]),
        ],
        ["access_token"]);
}

public class Step4Model(IConfiguration configuration) : StepModel(configuration)
{
    // offline_access is what this step ticks: "and give me a refresh token".
    public override ChallengeOptions Login => new(
        [("profile", true), ("email", true), (Apis.Scope, true), ("offline_access", false)], ["code"], [], []);

    public override ApiCallOptions Api => new(
        [
            ("Microsoft Graph", [Apis.Graph("/me/photo/$value"), Apis.Graph("/me")]),
            ("Kick-Ass Calendar API", [Apis.Calendar(Configuration, "/calendar")]),
        ],
        ["access_token", "refreshed_token"]);
}

public class Step5Model(IConfiguration configuration) : StepModel(configuration)
{
    // The trap is the order (the API first), then .default, then prompt=consent.
    public override ChallengeOptions Login => new(
        [("profile", true), ("email", true), (Apis.Scope, true), ("Calendars.Read", false), (Apis.DefaultScope, false)],
        ["code"], [], ["", "consent"]);

    public override ApiCallOptions Api => new(
        [("Microsoft Graph", [Apis.Graph("/me/events?$select=subject,start,end&$top=5")])],
        ["access_token", "obo_token"]);
}

public class Step6Model(IConfiguration configuration) : StepModel(configuration)
{
    public override ApiCallOptions Api => new(
        [("Kick-Ass Calendar API", [Apis.Calendar(Configuration, "/users/damon@kickassdev.se/calendar"), Apis.Calendar(Configuration, "/me")])],
        ["access_token"]);
}

public class Step7Model(IConfiguration configuration) : StepModel(configuration);

public class Step8Model(IConfiguration configuration) : StepModel(configuration)
{
    // Any organization's users: the authority is organizations, not my tenant.
    public override ChallengeOptions Login => new(
        [("profile", true), ("email", true), (Apis.DefaultScope, true)], ["code"], [], ["select_account", ""]);

    public override ApiCallOptions Api => new(
        [("Kick-Ass Calendar API", [Apis.Calendar(Configuration, "/calendar"), Apis.Calendar(Configuration, "/me")])],
        ["access_token"]);
}
