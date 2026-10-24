namespace Zkd.Demo.Azure.Auth.WhatReallyHappens.Pages.Shared;

// What a step's sign-in offers. The first option of each list is the default, and the checked scopes.
public record ChallengeOptions(
    IReadOnlyList<(string Scope, bool Checked)> Scopes,
    IReadOnlyList<string> ResponseTypes,
    IReadOnlyList<string> ResponseModes,
    IReadOnlyList<string> Prompts)
{
    // Scopes that are whole URIs show the last part. .default needs the part before it to say whose it is.
    public static string Label(string scope) =>
        scope.EndsWith("/.default") ? string.Join('/', scope.Split('/')[^2..]) : scope.Split('/')[^1];
}

// The tokens a step shows side by side. Validate: the one shared Split/Decode and a Validate on each, for a token the
// app checks itself. Otherwise each token decodes on its own.
public record TokenPairOptions(IReadOnlyList<string> Names, bool Validate = false);

// The calls a step offers, grouped by API, and the tokens it can send.
public record ApiCallOptions(IReadOnlyList<(string Group, IReadOnlyList<(string Label, string Url)> Calls)> Presets, IReadOnlyList<string> Tokens);

public static class TokenNames
{
    // What the step calls each token it holds. Some are access tokens from a second request.
    public static string Label(string name) => name switch
    {
        "refreshed_token" => "access_token, refreshed",
        "obo_token" => "access_token, On-Behalf-Of",
        _ => name,
    };
}

// The APIs the steps call, by their addresses.
public static class Apis
{
    public const string Scope = "https://kickassdev.se/api/KickAssCalendar.Read";
    public const string DefaultScope = "https://kickassdev.se/api/.default";
    public const string AppRoleScope = "https://kickassdev.se/api/KickAssCalendar.Read.All";

    public static (string Label, string Url) Graph(string path) => ($"Graph {path.Split('?')[0]}", $"https://graph.microsoft.com/v1.0{path}");

    public static (string Label, string Url) Calendar(IConfiguration configuration, string path) =>
        ($"Calendar API {path}", $"{configuration["services:calendar-api:https:0"]?.TrimEnd('/') ?? "https://localhost:7101"}{path}");
}
